// Volt City — the diorama driver (P3): P2's VcCityArt (built once, tier-aware) + the engine-free WorldModel diffs.
//   * light states (district ripple, corridors, plants, smart substations, storage gauges, light curve) are written ONLY
//     when the sim state changed — WorldModel.Sync runs when the sim step advanced or events arrived, compares small
//     integer arrays and returns changes; nothing is written on an unchanged step (Low: no per-frame CPU);
//   * crew trucks follow the road graph between the sim's nodes (Trucks: fractional-time, monotone), rewritten per frame on
//     Medium/High and at 15 Hz on Low, only while a truck moves; ambient cars at 15 Hz on Low (<= 20) / per frame on
//     Medium (<= 40); trees + rotors are written once;
//   * world taps come from the kit TapInput (release-only, < 12 dp / < 400 ms, pinch never selects, UI picks block):
//     ray -> Picker (fault > plant > district) -> Picked event. The guide gate is the UI's (Coach.Tap).
// UNCOMPILED until the Unity editor is installed.
using System;
using System.Collections.Generic;
using CoddyKit;
using UnityEngine;
using VoltCity.Art;
using VoltCity.Game;
using VoltCity.Sim;

namespace VoltCity.World
{
    public sealed class CityWorld : MonoBehaviour
    {
        [Header("Scene (VoltCitySceneBuilder wires these)")]
        public VcCityArt art;
        public VcLightCurves lights;
        public Camera cam;
        public CameraRig rig;
        public TapInput tap;
        public TextAsset layoutJson;

        public CityLayout Layout { get; private set; }
        public bool Low { get; private set; }
        public bool StormActive;
        /// <summary>A world tap resolved to fault / plant / district ("" kind = empty ground).</summary>
        public event Action<Picker.Hit> Picked;

        WorldModel model;
        Trucks trucks;
        Traffic traffic;
        RoadGraph roads;
        Simulation sim;
        int lastStep = int.MinValue;
        string lastPhase = "";
        bool dirty = true;
        float propAcc;
        readonly List<(int id, V3 pos)> faults = new List<(int, V3)>();
        int truckInstances, rotorLevel = -1;

        public const float LowPropHz = 15f;
        public const int CarsLow = 20, CarsMedium = 40;

        static Vector3 U(V3 v) => new Vector3((float)v.X, (float)v.Y, (float)v.Z);
        static V3 G(Vector3 v) => new V3(v.x, v.y, v.z);

        /// <summary>Builds the diorama for the tier (Low = P2's *Low shaders, half the trees, 20 cars, no shadows).</summary>
        public void Init(Simulation simulation, bool low)
        {
            sim = simulation;
            Low = low;
            Layout = CityLayout.Parse(layoutJson.text);
            roads = RoadGraph.From(sim.Gd.Raw["roads"]);
            model = new WorldModel(Layout);
            trucks = new Trucks(roads);
            art.low = low;
            art.Build();
            // trees: written once (Low: every second layout tree, ART-IDS)
            for (int i = 0; i < Layout.Trees.Count; i++)
            {
                if (low && (i & 1) == 1) continue;
                var t = Layout.Trees[i];
                float yaw = (float)((t.pos.X * 12.9898 + t.pos.Z * 78.233) % 360.0);
                art.Trees.Add(U(t.pos), yaw, 1f, Vector4.zero);
            }
            traffic = new Traffic(Layout, low ? CarsLow : CarsMedium, sim.S.Seed);
            foreach (var c in traffic.Cars) art.Cars.Add(U(c.Pos), (float)c.HeadingDeg, 1f, new Vector4(c.Shape, c.Paint, 0, 0));
            if (tap != null) tap.Tapped += OnTap;
            dirty = true;
            Tick(0f);
        }

        public void SetSim(Simulation simulation)
        {
            sim = simulation;
            dirty = true;
        }

        /// <summary>Events arrived (shed, fault, repaired, bought…): resync on the next tick even without a new step.</summary>
        public void MarkDirty() => dirty = true;

        string looksSig = "";

        /// <summary>P4 sold looks -> the P2 art uniforms (only when the equipped set changed).</summary>
        public void ApplyLooks(LookState l)
        {
            if (l == null || art == null || l.Sig() == looksSig) return;
            looksSig = l.Sig();
            art.SetLooks(Hex(l.LiveryA), Hex(l.LiveryB), l.BladeStyle, l.SkylineStyle, l.PylonDeco);
        }

        static Color Hex(string h) => ColorUtility.TryParseHtmlString("#" + h, out var c) ? c : Color.white;

        public void Lightning() { if (lights != null) lights.Flash(); }

        public void Tick(float dt)
        {
            if (sim == null || art == null) return;
            int step = sim.Grid != null ? sim.Grid.Step : -1;
            if (dirty || step != lastStep || sim.S.Phase != lastPhase)
            {
                var d = model.Sync(sim, StormActive);
                if (d.Any) Apply(d);
                lastStep = step;
                lastPhase = sim.S.Phase;
                dirty = false;
            }
            // trucks + cars: per frame on Medium / High, 15 Hz on Low
            propAcc += dt;
            float period = Low ? 1f / LowPropHz : 0f;
            if (propAcc < period) return;
            float pdt = propAcc;
            propAcc = 0f;
            UpdateTrucks();
            traffic.Step(pdt);
            for (int i = 0; i < traffic.Cars.Count; i++)
            {
                var c = traffic.Cars[i];
                art.Cars.Set(i, U(c.Pos), (float)c.HeadingDeg, 1f, new Vector4(c.Shape, c.Paint, 0, 0));
            }
        }

        void UpdateTrucks()
        {
            int n = trucks.Update(sim);
            if (n != truckInstances)
            {
                art.Trucks.Clear();
                for (int i = 0; i < n; i++) art.Trucks.Add(Vector3.zero, 0f, 1f, Vector4.zero);
                truckInstances = n;
            }
            for (int i = 0; i < n; i++)
            {
                var tr = trucks.Tracks[i];
                art.Trucks.Set(i, U(tr.Pos) + new Vector3(0f, 0.2f, 0f), (float)tr.HeadingDeg, 1f, new Vector4(tr.Boom ? 1f : 0f, 0, 0, 0));
            }
        }

        void Apply(WorldDiff d)
        {
            foreach (var (id, isLit, animate) in d.Districts) art.SetDistrictLit(id, isLit, animate);
            foreach (var (index, state, flow) in d.Corridors) art.SetCorridor(index, state, flow);
            if (d.PlantLevels != null)
            {
                art.SetPlantLevels(d.PlantLevels);
                int wind = d.PlantLevels.TryGetValue("wind", out int w) ? w : 0;
                if (wind != rotorLevel) SetRotors(wind);
            }
            foreach (var (type, output) in d.PlantOutputs) art.SetPlantOutput(type, output);
            if (d.Smart != null) art.SetSmartSubstations(d.Smart);
            if (d.Faults != null)
            {
                faults.Clear();
                faults.AddRange(d.Faults);
                art.Markers.Clear();
                foreach (var (id, pos) in faults) art.Markers.Add(U(pos), 0f, 1f, new Vector4((id * 0.37f) % 1f, 0, 0, 0));
            }
            if (d.Battery >= 0) Shader.SetGlobalFloat("_VcBatteryCharge", d.Battery);
            if (d.Reservoir >= 0) Shader.SetGlobalFloat("_VcReservoirLevel", d.Reservoir);
            if (d.Moment != null && lights != null)
            {
                switch (d.Moment)
                {
                    case "day": lights.Apply(VcLightCurves.Day); break;
                    case "dusk": lights.Apply(VcLightCurves.Dusk); break;
                    case "storm": lights.Apply(VcLightCurves.Storm); break;
                    default: lights.Apply(VcLightCurves.NightM); break;
                }
            }
            if (d.Districts.Count > 0 && rig != null) rig.Refit(LitBoxes());
        }

        void SetRotors(int level)
        {
            rotorLevel = level;
            art.Rotors.Clear();
            int n = Math.Min(Layout.WindHubs.Count, 8 * Math.Max(0, level));
            for (int i = 0; i < n; i++)
                art.Rotors.Add(U(Layout.WindHubs[i]), 0f, 1f, new Vector4(1.3f, (i * 0.618f % 1f) * 6.2831f, 0, 0));
        }

        /// <summary>Boxes of the districts that are hooked up (the camera fits these, not the scene bounds — UI.md section 1).</summary>
        public List<Bounds> LitBoxes()
        {
            var o = new List<Bounds>();
            foreach (var d in Layout.Districts)
            {
                if (!sim.S.IsHooked(d.Id)) continue;
                var b = new Bounds();
                b.SetMinMax(U(d.Min), U(d.Max));
                o.Add(b);
            }
            if (o.Count == 0) foreach (var d in Layout.Districts) { var b = new Bounds(); b.SetMinMax(U(d.Min), U(d.Max)); o.Add(b); }
            return o;
        }

        void OnTap(Vector2 topLeftPx)
        {
            if (cam == null || sim == null) return;
            var ray = cam.ScreenPointToRay(new Vector3(topLeftPx.x, Screen.height - topLeftPx.y, 0f));
            var hit = Picker.Pick(Layout, G(ray.origin), G(ray.direction.normalized), faults, p => sim.S.Level(GameData.Plants[p]) > 0);
            Picked?.Invoke(hit);
        }

        /// <summary>Screen position (top-left px) of a fault marker for the guide's spotlight; false when off screen.</summary>
        public bool FaultScreen(int faultId, out Vector2 topLeftPx, out float radiusPx)
        {
            topLeftPx = default;
            radiusPx = 0f;
            foreach (var (id, pos) in faults)
            {
                if (id != faultId) continue;
                var p = cam.WorldToScreenPoint(U(pos));
                if (p.z <= 0) return false;
                var edge = cam.WorldToScreenPoint(U(pos) + cam.transform.right * (float)Picker.FaultRadius);
                radiusPx = Mathf.Max(48f, Mathf.Abs(edge.x - p.x));
                topLeftPx = new Vector2(p.x, Screen.height - p.y);
                return p.x >= 0 && p.x <= Screen.width && p.y >= 0 && p.y <= Screen.height;
            }
            return false;
        }

        void OnDestroy()
        {
            if (tap != null) tap.Tapped -= OnTap;
        }
    }
}
