// Volt City — the diorama's logic without the engine (P3): city layout (P2 layout.json), the sim -> art mapping
// (8 sim corridors -> art corridors 0-7, plant ties 8-13, loops 14-15; ART-IDS), the change-only world state (district
// light ripple, corridor states, plant levels / outputs, smart substations, fault markers, storage gauges), crew truck
// tracks on the road graph (fractional-time progress, never backwards: LL Disaster W66), ambient traffic, and ray
// picking (fault > plant > district). The Unity layer (VoltCity.World) only copies the diffs into VcCityArt.
// Space: Unity metres, x east, y up, z north (roads.json nodes are [x, z, y]).
using System;
using System.Collections.Generic;
using VoltCity.Sim;

namespace VoltCity.Game
{
    public struct V3
    {
        public double X, Y, Z;
        public V3(double x, double y, double z) { X = x; Y = y; Z = z; }
        public static V3 operator +(V3 a, V3 b) => new V3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static V3 operator -(V3 a, V3 b) => new V3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static V3 operator *(V3 a, double k) => new V3(a.X * k, a.Y * k, a.Z * k);
        public double Dot(V3 b) => X * b.X + Y * b.Y + Z * b.Z;
        public double LenXZ => Math.Sqrt(X * X + Z * Z);
        public double Len => Math.Sqrt(X * X + Y * Y + Z * Z);
        public static V3 Lerp(V3 a, V3 b, double t) => a + (b - a) * t;
        public override string ToString() => $"({X:0.#}, {Y:0.#}, {Z:0.#})";
    }

    // ============================================================================================= layout

    public sealed class CityLayout
    {
        public sealed class District { public string Id; public V3 Min, Max, Substation; public double RippleRadius; }
        public sealed class Corridor { public int Index; public string Id, A, B; public List<V3> Points = new List<V3>(); public V3 Mid; }

        /// <summary>Art plant type names in the art's index order = the sim's plant order (steam, gas->peaker, …).</summary>
        public static readonly string[] ArtPlants = { "steam", "peaker", "hydro", "wind", "solar", "battery" };

        public readonly List<District> Districts = new List<District>();
        public readonly Dictionary<string, District> DistrictById = new Dictionary<string, District>();
        public readonly List<Corridor> Corridors = new List<Corridor>();
        public readonly Dictionary<string, V3> PlantSite = new Dictionary<string, V3>();   // art type -> site
        public readonly List<V3> WindHubs = new List<V3>();
        public readonly List<List<V3>> Roads = new List<List<V3>>();
        public readonly List<(V3 pos, string district)> Trees = new List<(V3, string)>();
        public V3 BoundsMin, BoundsMax;

        static V3 P(object a)
        {
            var l = a as List<object>;
            return l == null || l.Count < 3 ? new V3() : new V3(J.D(l[0]), J.D(l[1]), J.D(l[2]));
        }

        public static CityLayout Parse(string json)
        {
            var o = Json.TryParse(json) as JObj ?? throw new FormatException("layout.json is not an object");
            var L = new CityLayout();
            foreach (var d in J.Arr(o, "districts"))
            {
                var x = new District
                {
                    Id = J.S(d, "id"), Min = P(J.Get(d, "box_min")), Max = P(J.Get(d, "box_max")), Substation = P(J.Get(d, "substation")),
                    RippleRadius = J.D(d, "ripple_radius"),
                };
                L.Districts.Add(x);
                L.DistrictById[x.Id] = x;
            }
            foreach (var c in J.Arr(o, "corridors"))
            {
                var x = new Corridor { Index = J.I(c, "index"), Id = J.S(c, "id"), A = J.S(c, "a"), B = J.S(c, "b") };
                foreach (var p in J.Arr(c, "points"))
                {
                    var l = p as List<object>;
                    x.Points.Add(new V3(J.D(l[0]), 0, J.D(l[1])));
                }
                x.Mid = PolylineAt(x.Points, 0.5);
                L.Corridors.Add(x);
            }
            L.Corridors.Sort((a, b) => a.Index.CompareTo(b.Index));
            var plants = J.Obj(o, "plants");
            foreach (var k in plants.Keys)
            {
                L.PlantSite[k] = P(J.Get(plants[k], "site"));
                if (k == "wind" && J.Arr(plants[k], "hubs") is List<object> hubs) foreach (var h in hubs) L.WindHubs.Add(P(h));
            }
            foreach (var r in J.Arr(o, "roads"))
            {
                var pts = new List<V3>();
                foreach (var p in J.Arr(r, "centre")) pts.Add(P(p));
                L.Roads.Add(pts);
            }
            foreach (var t in J.Arr(o, "trees"))
            {
                var l = t as List<object>;
                L.Trees.Add((new V3(J.D(l[0]), J.D(l[1]), J.D(l[2])), l.Count > 3 ? J.Str(l[3]) : ""));
            }
            L.BoundsMin = new V3(double.MaxValue, double.MaxValue, double.MaxValue);
            L.BoundsMax = new V3(double.MinValue, double.MinValue, double.MinValue);
            foreach (var d in L.Districts) { L.Grow(d.Min); L.Grow(d.Max); }
            foreach (var s in L.PlantSite.Values) L.Grow(s);
            // corridor midpoints sit on the ground at the nearest district pad height
            foreach (var c in L.Corridors)
            {
                double y = 0;
                double best = double.MaxValue;
                foreach (var d in L.Districts)
                {
                    double dd = (d.Substation - c.Mid).LenXZ;
                    if (dd < best) { best = dd; y = d.Substation.Y; }
                }
                c.Mid = new V3(c.Mid.X, y, c.Mid.Z);
            }
            return L;
        }

        void Grow(V3 p)
        {
            BoundsMin = new V3(Math.Min(BoundsMin.X, p.X), Math.Min(BoundsMin.Y, p.Y), Math.Min(BoundsMin.Z, p.Z));
            BoundsMax = new V3(Math.Max(BoundsMax.X, p.X), Math.Max(BoundsMax.Y, p.Y), Math.Max(BoundsMax.Z, p.Z));
        }

        public static V3 PolylineAt(List<V3> pts, double t)
        {
            if (pts.Count == 0) return new V3();
            if (pts.Count == 1) return pts[0];
            double total = 0;
            for (int i = 0; i + 1 < pts.Count; i++) total += (pts[i + 1] - pts[i]).Len;
            double want = Math.Max(0, Math.Min(1, t)) * total, acc = 0;
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                double L = (pts[i + 1] - pts[i]).Len;
                if (acc + L >= want) return V3.Lerp(pts[i], pts[i + 1], L > 0 ? (want - acc) / L : 0);
                acc += L;
            }
            return pts[pts.Count - 1];
        }
    }

    // ============================================================================================= sim -> art map

    public static class ArtMap
    {
        /// <summary>Sim corridor id -> art corridor index (SIM-API section 3, ART-IDS corridors).</summary>
        public static readonly Dictionary<string, int> Corridor = new Dictionary<string, int>
        {
            { "c_harbor", 0 }, { "c_riverside", 1 }, { "c_market", 2 }, { "c_hilltop", 3 },
            { "c_hospital", 4 }, { "c_tech", 5 }, { "c_stadium", 6 }, { "c_airport", 7 },
        };
        /// <summary>Plant tie corridor of sim plant index p (steam_tie 8 … battery_tie 13).</summary>
        public static int PlantTie(int p) => 8 + p;
        public const int EastLoop = 14, WestLoop = 15;
        public const int Corridors = 16;
        /// <summary>Art corridor states (VcWorldCore: _VcCorridorState.x).</summary>
        public const int Normal = 0, Overheat = 1, Down = 2, NotBuilt = 3;
        /// <summary>Smart substation tie -> the two districts whose substation becomes the smart model.</summary>
        public static readonly Dictionary<string, string[]> TieDistricts = new Dictionary<string, string[]>
        {
            { "sub_1", new[] { "old_harbor", "riverside" } },
            { "sub_2", new[] { "market_square", "stadium_row" } },
            { "sub_3", new[] { "hospital_hill", "hilltop" } },
        };
    }

    // ============================================================================================= world state diff

    public sealed class WorldDiff
    {
        public readonly List<(string id, bool lit, bool animate)> Districts = new List<(string, bool, bool)>();
        public readonly List<(int index, int state, float flow)> Corridors = new List<(int, int, float)>();
        /// <summary>Art type -> level, null when unchanged.</summary>
        public Dictionary<string, int> PlantLevels;
        public readonly List<(int type, float output)> PlantOutputs = new List<(int, float)>();
        /// <summary>District ids with a smart substation, null when unchanged.</summary>
        public HashSet<string> Smart;
        /// <summary>Open fault markers (id, position), null when unchanged.</summary>
        public List<(int id, V3 pos)> Faults;
        public float Battery = -1f, Reservoir = -1f;
        /// <summary>Light curve moment: day | dusk | night | storm; null when unchanged.</summary>
        public string Moment;
        public bool Any => Districts.Count > 0 || Corridors.Count > 0 || PlantLevels != null || PlantOutputs.Count > 0 || Smart != null || Faults != null
                           || Battery >= 0 || Reservoir >= 0 || Moment != null;
    }

    public sealed class WorldModel
    {
        readonly CityLayout layout;
        readonly Dictionary<string, bool> lit = new Dictionary<string, bool>();
        readonly int[] corrState = new int[ArtMap.Corridors];
        readonly int[] corrFlow = new int[ArtMap.Corridors];   // tenths
        readonly int[] plantOut = new int[6];                  // tenths
        string plantSig = "", smartSig = "", faultSig = "", moment = "";
        int battery = -1, reservoir = -1;                      // twentieths
        bool first = true;

        public WorldModel(CityLayout layout)
        {
            this.layout = layout;
            for (int i = 0; i < ArtMap.Corridors; i++) { corrState[i] = -1; corrFlow[i] = -1; }
            for (int i = 0; i < 6; i++) plantOut[i] = -1;
        }

        /// <summary>Light curve moment of a diorama hour (storm while a thunderstorm runs).</summary>
        public static string MomentAt(double hour, bool storm)
        {
            if (storm) return "storm";
            if (hour >= 6.5 && hour < 17.5) return "day";
            if ((hour >= 17.5 && hour < 19.5) || (hour >= 5 && hour < 6.5)) return "dusk";
            return "night";
        }

        /// <summary>Compares the sim with what the diorama shows and returns only the changes. Call when the sim step
        /// changed or events arrived (never per frame on Low; WorldDriver throttles).</summary>
        public WorldDiff Sync(Simulation sim, bool stormActive)
        {
            var d = new WorldDiff();
            var s = sim.S;
            var g = sim.Grid;
            var gd = sim.Gd;
            // districts
            foreach (var def in gd.Districts)
            {
                bool on;
                if (!s.IsHooked(def.Id)) on = false;
                else if (g == null) on = true;                         // between shifts the city rests lit
                else
                {
                    int i = Array.IndexOf(g.DIds, def.Id);
                    on = i >= 0 && g.IsLit(i) && g.DPart[i] < 500;
                }
                if (!lit.TryGetValue(def.Id, out bool was) || was != on)
                {
                    d.Districts.Add((def.Id, on, !first));
                    lit[def.Id] = on;
                }
            }
            // city corridors 0-7
            for (int c = 0; c < gd.Corridors.Count; c++)
            {
                var cd = gd.Corridors[c];
                if (!ArtMap.Corridor.TryGetValue(cd.Id, out int idx)) continue;
                int st, fl = 0;
                if (!s.IsHooked(cd.District)) st = ArtMap.NotBuilt;
                else if (g == null) st = ArtMap.Normal;
                else
                {
                    int ci = Array.IndexOf(g.CIds, cd.Id);
                    if (ci < 0) st = ArtMap.NotBuilt;
                    else if (g.CFault[ci] >= 0 || g.COutUntil[ci] > g.Step) st = ArtMap.Down;
                    else if (g.CHeat[ci] >= gd.LineWarnPct * 10000) st = ArtMap.Overheat;
                    else st = ArtMap.Normal;
                    if (ci >= 0 && g.CCap[ci] > 0) fl = (int)Math.Round(Math.Min(1.0, Math.Abs((double)g.CFlow[ci]) / g.CCap[ci]) * 10);
                }
                SetCorr(d, idx, st, fl);
            }
            // plant ties 8-13 + loops
            for (int p = 0; p < 6; p++)
            {
                int lv = s.Level(GameData.Plants[p]);
                int fl = g != null && g.PCap[p] > 0 ? (int)Math.Round(Math.Min(1.0, Math.Abs((double)g.POut[p]) / g.PCap[p]) * 10) : (lv > 0 ? 5 : 0);
                SetCorr(d, ArtMap.PlantTie(p), lv > 0 ? ArtMap.Normal : ArtMap.NotBuilt, fl);
            }
            SetCorr(d, ArtMap.EastLoop, s.IsHooked("tech_park") ? ArtMap.Normal : ArtMap.NotBuilt, 3);
            SetCorr(d, ArtMap.WestLoop, s.IsHooked("stadium_row") ? ArtMap.Normal : ArtMap.NotBuilt, 3);
            // plant levels (art type names) + outputs
            var levels = new Dictionary<string, int>();
            var sig = new System.Text.StringBuilder();
            for (int p = 0; p < 6; p++)
            {
                int lv = s.Level(GameData.Plants[p]);
                levels[CityLayout.ArtPlants[p]] = lv;
                sig.Append(lv);
            }
            if (sig.ToString() != plantSig) { plantSig = sig.ToString(); d.PlantLevels = levels; }
            for (int p = 0; p < 6; p++)
            {
                int o;
                if (s.Level(GameData.Plants[p]) == 0) o = 0;
                else if (g == null) o = p == Grid.PSteam || p == Grid.PHydro || p == Grid.PWind || p == Grid.PSolar ? 5 : 0;
                else o = g.POut[p] > 0 ? Math.Max(1, (int)Math.Round(Math.Min(1.0, (double)g.POut[p] / Math.Max(1, g.PCap[p])) * 10)) : 0;
                if (plantOut[p] != o) { plantOut[p] = o; d.PlantOutputs.Add((p, o / 10f)); }
            }
            // smart substations
            var smart = new HashSet<string>();
            foreach (var t in s.Ties) if (ArtMap.TieDistricts.TryGetValue(t, out var ds)) foreach (var x in ds) smart.Add(x);
            var ss = new List<string>(smart);
            ss.Sort(StringComparer.Ordinal);
            string smartS = string.Join(",", ss);
            if (smartS != smartSig) { smartSig = smartS; d.Smart = smart; }
            // fault markers
            var faults = new List<(int, V3)>();
            if (g != null)
                foreach (var f in g.Faults)
                    if (f.Open) faults.Add((f.Id, FaultPos(g, f)));
            var fs = new System.Text.StringBuilder();
            foreach (var f in faults) fs.Append(f.Item1).Append(',');
            if (fs.ToString() != faultSig) { faultSig = fs.ToString(); d.Faults = faults; }
            // storage gauges (twentieths)
            int bat = g != null && g.BatCapWh > 0 ? (int)(g.BatSoc * 20 / g.BatCapWh) : (s.Level("battery") > 0 ? 12 : 0);
            if (bat != battery) { battery = bat; d.Battery = bat / 20f; }
            int res = g != null && g.ResCapWh > 0 ? (int)(g.ResWh * 20 / g.ResCapWh) : 16;
            if (res != reservoir) { reservoir = res; d.Reservoir = res / 20f; }
            // light curve
            string m = MomentAt(g != null ? g.HourOfDay : 18.5, stormActive);
            if (m != moment) { moment = m; d.Moment = m; }
            first = false;
            return d;
        }

        void SetCorr(WorldDiff d, int idx, int st, int fl)
        {
            if (corrState[idx] == st && corrFlow[idx] == fl) return;
            corrState[idx] = st;
            corrFlow[idx] = fl;
            d.Corridors.Add((idx, st, fl / 10f));
        }

        /// <summary>Where a fault's spark sits: the middle of its art corridor (line) or the district substation (transformer).</summary>
        public V3 FaultPos(Grid g, Fault f)
        {
            if (f.Kind == "line" && f.Corridor >= 0 && ArtMap.Corridor.TryGetValue(g.CIds[f.Corridor], out int idx) && idx < layout.Corridors.Count)
                return layout.Corridors[idx].Mid + new V3(0, 14, 0);
            string dist = f.District >= 0 ? g.DIds[f.District] : "";
            return layout.DistrictById.TryGetValue(dist, out var dd) ? dd.Substation + new V3(0, 10, 0) : new V3();
        }
    }

    // ============================================================================================= road graph + trucks

    public sealed class RoadGraph
    {
        public readonly Dictionary<string, V3> Nodes = new Dictionary<string, V3>();
        readonly Dictionary<string, List<(string to, double len)>> adj = new Dictionary<string, List<(string, double)>>();
        public string Depot = "depot";

        /// <summary>From GameData.Raw["roads"] (nodes [x, z, y]).</summary>
        public static RoadGraph From(JObj roads)
        {
            var r = new RoadGraph { Depot = J.S(roads, "depot", "depot") };
            var nodes = J.Obj(roads, "nodes");
            foreach (var k in nodes.Keys)
            {
                var l = nodes[k] as List<object>;
                r.Nodes[k] = new V3(J.D(l[0]), l.Count > 2 ? J.D(l[2]) : 0, J.D(l[1]));
                r.adj[k] = new List<(string, double)>();
            }
            foreach (var e in J.Arr(roads, "edges"))
            {
                var p = e as List<object>;
                string a = J.Str(p[0]), b = J.Str(p[1]);
                if (!r.Nodes.ContainsKey(a) || !r.Nodes.ContainsKey(b)) continue;
                double len = (r.Nodes[a] - r.Nodes[b]).LenXZ;
                r.adj[a].Add((b, len));
                r.adj[b].Add((a, len));
            }
            foreach (var k in r.adj.Keys) r.adj[k].Sort((x, y) => string.CompareOrdinal(x.to, y.to));
            return r;
        }

        /// <summary>Shortest path (node positions) a -> b; deterministic tie-break by node name.</summary>
        public List<V3> Path(string a, string b)
        {
            var o = new List<V3>();
            if (!Nodes.ContainsKey(a ?? "") || !Nodes.ContainsKey(b ?? "")) return o;
            var dist = new Dictionary<string, double> { [a] = 0 };
            var prev = new Dictionary<string, string>();
            var open = new List<string> { a };
            var done = new HashSet<string>();
            while (open.Count > 0)
            {
                int bi = 0;
                for (int i = 1; i < open.Count; i++)
                    if (dist[open[i]] < dist[open[bi]] || (dist[open[i]] == dist[open[bi]] && string.CompareOrdinal(open[i], open[bi]) < 0)) bi = i;
                string u = open[bi];
                open.RemoveAt(bi);
                if (!done.Add(u)) continue;
                if (u == b) break;
                foreach (var (to, len) in adj[u])
                {
                    double nd = dist[u] + len;
                    if (!dist.TryGetValue(to, out double old) || nd < old - 1e-9)
                    {
                        dist[to] = nd;
                        prev[to] = u;
                        if (!done.Contains(to)) open.Add(to);
                    }
                }
            }
            if (!dist.ContainsKey(b)) return o;
            var names = new List<string>();
            for (string c = b; c != null; c = prev.TryGetValue(c, out var pc) ? pc : null) names.Add(c);
            names.Reverse();
            foreach (var n in names) o.Add(Nodes[n]);
            return o;
        }
    }

    /// <summary>One crew truck's visible motion. The sim owns the arrival step; this only interpolates between road nodes.</summary>
    public sealed class TruckTrack
    {
        public List<V3> Path = new List<V3>();
        public double Start, End;           // sim steps (fractional)
        public V3 Pos;
        public double HeadingDeg;
        public bool Boom;                   // bucket raised while repairing
        public string LegKey = "";
        double lastT;

        public static double Length(List<V3> p)
        {
            double L = 0;
            for (int i = 0; i + 1 < p.Count; i++) L += (p[i + 1] - p[i]).LenXZ;
            return L;
        }

        /// <summary>Starts a new leg from where the truck is NOW (a retarget never jumps back along the road).</summary>
        public void NewLeg(string key, List<V3> path, double start, double end, bool hasPos)
        {
            LegKey = key;
            if (hasPos && path.Count > 0 && (path[0] - Pos).LenXZ > 1.0)
            {
                // project the current position onto the new path; start from the projection (or prepend it when far)
                int bestI = 0; double bestD = double.MaxValue, bestT = 0;
                for (int i = 0; i + 1 < path.Count; i++)
                {
                    var a = path[i]; var b = path[i + 1];
                    var ab = b - a; double L2 = ab.X * ab.X + ab.Z * ab.Z;
                    double t = L2 > 0 ? Math.Max(0, Math.Min(1, ((Pos.X - a.X) * ab.X + (Pos.Z - a.Z) * ab.Z) / L2)) : 0;
                    var q = V3.Lerp(a, b, t);
                    double dd = (q - Pos).LenXZ;
                    if (dd < bestD) { bestD = dd; bestI = i; bestT = t; }
                }
                var np = new List<V3> { Pos };
                if (bestD <= 25.0) np.Add(V3.Lerp(path[bestI], path[bestI + 1], bestT));
                for (int i = bestD <= 25.0 ? bestI + 1 : 0; i < path.Count; i++) np.Add(path[i]);
                path = np;
            }
            Path = path;
            Start = start;
            End = Math.Max(start + 1e-3, end);
            lastT = 0;
            if (!hasPos && path.Count > 0) Pos = path[0];
        }

        public void Park(string key, V3 at, bool boom)
        {
            LegKey = key;
            Path = new List<V3> { at };
            Pos = at;
            Boom = boom;
            Start = End = 0;
            lastT = 1;
        }

        /// <summary>Position at sim time stepF (fractional steps). Progress is monotone (no backward correction).</summary>
        public V3 At(double stepF)
        {
            if (Path.Count == 0) return Pos;
            if (Path.Count == 1) { Pos = Path[0]; return Pos; }
            double t = Math.Max(0, Math.Min(1, (stepF - Start) / (End - Start)));
            if (t < lastT) t = lastT;
            lastT = t;
            double total = Length(Path), want = t * total, acc = 0;
            for (int i = 0; i + 1 < Path.Count; i++)
            {
                double L = (Path[i + 1] - Path[i]).LenXZ;
                if (acc + L >= want || i + 2 == Path.Count)
                {
                    double u = L > 0 ? Math.Min(1, (want - acc) / L) : 1;
                    Pos = V3.Lerp(Path[i], Path[i + 1], u);
                    var dir = Path[i + 1] - Path[i];
                    if (dir.LenXZ > 0.01) HeadingDeg = Math.Atan2(dir.X, dir.Z) * 180.0 / Math.PI;
                    break;
                }
                acc += L;
            }
            return Pos;
        }
    }

    public sealed class Trucks
    {
        readonly RoadGraph graph;
        public readonly List<TruckTrack> Tracks = new List<TruckTrack>();

        public Trucks(RoadGraph graph) { this.graph = graph; }

        /// <summary>Fractional sim step now (whole steps + the waiting remainder).</summary>
        public static double StepF(Simulation sim) =>
            sim.Grid == null ? 0 : sim.Grid.Step + Math.Max(0, Math.Min(0.999, (double)sim.S.AccUs / Math.Max(1, sim.Gd.StepUs)));

        /// <summary>Updates legs from the sim (cheap: compares a key per truck) and positions. Returns the truck count.</summary>
        public int Update(Simulation sim)
        {
            var g = sim.Grid;
            int n = g != null ? g.Crews.Count : sim.S.Crews;
            while (Tracks.Count < n) Tracks.Add(new TruckTrack());
            while (Tracks.Count > n) Tracks.RemoveAt(Tracks.Count - 1);
            double now = StepF(sim);
            for (int i = 0; i < n; i++)
            {
                var tr = Tracks[i];
                bool hasPos = tr.LegKey != "";
                if (g == null)
                {
                    var depot = graph.Nodes.TryGetValue(graph.Depot, out var dp) ? dp : new V3();
                    string k = "park:depot:" + i;
                    if (tr.LegKey != k) tr.Park(k, depot + new V3(i * 7.0, 0, 0), false);
                    continue;
                }
                var c = g.Crews[i];
                switch (c.State)
                {
                    case CrewTruck.Travel:
                    {
                        var f = g.FindFault(c.Job);
                        string to = f != null ? f.Site : c.Node;
                        string key = "go:" + c.Node + ">" + to + "@" + c.Until;
                        if (tr.LegKey != key)
                        {
                            double dur = g.TravelSteps(c.Node, to);
                            tr.NewLeg(key, graph.Path(c.Node, to), c.Until - dur, c.Until, hasPos);
                        }
                        tr.Boom = false;
                        tr.At(now);
                        break;
                    }
                    case CrewTruck.Return:
                    {
                        string key = "back:" + c.Node + "@" + c.Until;
                        if (tr.LegKey != key)
                        {
                            double dur = g.TravelSteps(c.Node, sim.Gd.Depot);
                            tr.NewLeg(key, graph.Path(c.Node, sim.Gd.Depot), c.Until - dur, c.Until, hasPos);
                        }
                        tr.Boom = false;
                        tr.At(now);
                        break;
                    }
                    default:
                    {
                        string key = "park:" + c.Node + ":" + c.State + ":" + i;
                        if (tr.LegKey != key)
                        {
                            var at = graph.Nodes.TryGetValue(c.Node ?? "", out var np) ? np : new V3();
                            if (c.Node == sim.Gd.Depot) at += new V3(i * 7.0, 0, 0);   // trucks side by side at the depot
                            tr.Park(key, at, c.State == CrewTruck.Repair);
                        }
                        break;
                    }
                }
            }
            return n;
        }
    }

    // ============================================================================================= ambient traffic

    public sealed class Traffic
    {
        public struct Car { public int Road; public double S, Speed; public int Dir; public float Shape, Paint; public V3 Pos; public double HeadingDeg; }
        readonly CityLayout layout;
        readonly double[] lengths;
        public readonly List<Car> Cars = new List<Car>();
        public const double Lane = 1.8;

        public Traffic(CityLayout layout, int count, long seed)
        {
            this.layout = layout;
            lengths = new double[layout.Roads.Count];
            for (int i = 0; i < layout.Roads.Count; i++) lengths[i] = TruckTrack.Length(layout.Roads[i]);
            ulong x = (ulong)(seed * 2654435761L + 1);
            double R() { x ^= x << 13; x ^= x >> 7; x ^= x << 17; return (x % 1000000UL) / 1000000.0; }
            for (int k = 0; k < count && layout.Roads.Count > 0; k++)
            {
                int road = k % layout.Roads.Count;
                Cars.Add(new Car
                {
                    Road = road, S = R() * lengths[road], Speed = 8 + R() * 6, Dir = R() < 0.5 ? 1 : -1,
                    Shape = (float)Math.Floor(R() * 3), Paint = (float)Math.Floor(R() * 8),
                });
            }
            Step(0);
        }

        /// <summary>Moves every car dt seconds (the World layer calls it at 15 Hz on Low, per frame on Medium/High).</summary>
        public void Step(double dt)
        {
            for (int i = 0; i < Cars.Count; i++)
            {
                var c = Cars[i];
                double L = lengths[c.Road];
                if (L <= 0) continue;
                c.S += c.Dir * c.Speed * dt;
                if (c.S > L) { c.S = L; c.Dir = -1; }
                if (c.S < 0) { c.S = 0; c.Dir = 1; }
                var pts = layout.Roads[c.Road];
                var p = CityLayout.PolylineAt(pts, c.S / L);
                var q = CityLayout.PolylineAt(pts, Math.Min(1, (c.S + 1) / L));
                var dir = q - p;
                if (dir.LenXZ < 1e-6) dir = p - CityLayout.PolylineAt(pts, Math.Max(0, (c.S - 1) / L));
                double len = Math.Max(1e-6, dir.LenXZ);
                var side = new V3(dir.Z / len, 0, -dir.X / len);       // right of the road's direction
                c.Pos = p + side * (Lane * c.Dir) + new V3(0, 0.3, 0);
                c.HeadingDeg = Math.Atan2(dir.X * c.Dir, dir.Z * c.Dir) * 180.0 / Math.PI;
                Cars[i] = c;
            }
        }
    }

    // ============================================================================================= picking

    public static class Picker
    {
        public const double FaultRadius = 30, PlantRadius = 48;

        public sealed class Hit { public string Kind = ""; public string Id = ""; public double T = double.MaxValue; }

        static double RaySphere(V3 o, V3 d, V3 c, double r)
        {
            var oc = o - c;
            double b = oc.Dot(d), cc = oc.Dot(oc) - r * r, h = b * b - cc;
            if (h < 0) return -1;
            double t = -b - Math.Sqrt(h);
            return t >= 0 ? t : (-b + Math.Sqrt(h) >= 0 ? 0 : -1);
        }

        static double RayBox(V3 o, V3 d, V3 mn, V3 mx)
        {
            double t0 = 0, t1 = double.MaxValue;
            double[] O = { o.X, o.Y, o.Z }, D = { d.X, d.Y, d.Z }, A = { mn.X, mn.Y, mn.Z }, B = { mx.X, mx.Y, mx.Z };
            for (int i = 0; i < 3; i++)
            {
                if (Math.Abs(D[i]) < 1e-12) { if (O[i] < A[i] || O[i] > B[i]) return -1; continue; }
                double ta = (A[i] - O[i]) / D[i], tb = (B[i] - O[i]) / D[i];
                if (ta > tb) { var tmp = ta; ta = tb; tb = tmp; }
                t0 = Math.Max(t0, ta);
                t1 = Math.Min(t1, tb);
                if (t0 > t1) return -1;
            }
            return t0;
        }

        /// <summary>Ray (origin, unit direction) -> fault > plant > district (faults win when the ray passes their sphere,
        /// so a spark in front of a district is always the pick).</summary>
        public static Hit Pick(CityLayout L, V3 o, V3 d, IList<(int id, V3 pos)> faults, Func<int, bool> plantBuilt)
        {
            var hit = new Hit();
            if (faults != null)
                foreach (var (id, pos) in faults)
                {
                    double t = RaySphere(o, d, pos, FaultRadius);
                    if (t >= 0 && (hit.Kind != "fault" || t < hit.T)) { hit.Kind = "fault"; hit.Id = id.ToString(System.Globalization.CultureInfo.InvariantCulture); hit.T = t; }
                }
            if (hit.Kind == "fault") return hit;
            for (int p = 0; p < CityLayout.ArtPlants.Length; p++)
            {
                if (plantBuilt != null && !plantBuilt(p)) continue;
                if (!L.PlantSite.TryGetValue(CityLayout.ArtPlants[p], out var site)) continue;
                double t = RaySphere(o, d, site + new V3(0, 15, 0), PlantRadius);
                if (t >= 0 && t < hit.T) { hit.Kind = "plant"; hit.Id = GameData.Plants[p]; hit.T = t; }
            }
            if (hit.Kind == "plant") return hit;
            foreach (var dd in L.Districts)
            {
                double t = RayBox(o, d, dd.Min, dd.Max);
                if (t >= 0 && t < hit.T) { hit.Kind = "district"; hit.Id = dd.Id; hit.T = t; }
            }
            return hit;
        }
    }
}
