// Bottle Route sim core — engine independent. Road network: all-pairs shortest paths (Floyd–Warshall; the map has ~30
// nodes) in two variants: every road, and big-vehicle roads only (alleys closed). Computed once from the data, never per
// frame (fleet perf rule: route planning runs when the plan changes).
// Chapter 7: an edge can be a FERRY crossing (map flag "ferry", pier to pier). It is part of the shortest paths like any
// road (the island has no other way in); per pair the graph also keeps the ferry km on that path, so fuel is charged only
// for road km and the fleet knows where a truck must wait for a departure (Fleet.cs).
using System;
using System.Collections.Generic;

namespace BottleRoute.Sim
{
    public sealed class RoadGraph
    {
        public const double Inf = 1e18;
        readonly int n;
        readonly double[][] dist = new double[2][];
        readonly int[][] next = new int[2][];
        readonly double[][] ferryKm = new double[2][];
        readonly bool[] ferry;
        /// <summary>The map has at least one ferry crossing.</summary>
        public readonly bool HasFerry;
        public readonly GameData Gd;

        public RoadGraph(GameData gd)
        {
            Gd = gd;
            n = gd.Nodes.Count;
            ferry = new bool[n * n];
            foreach (var e in gd.Edges) if (e.Ferry) { ferry[e.A * n + e.B] = ferry[e.B * n + e.A] = true; HasFerry = true; }
            for (int v = 0; v < 2; v++)
            {
                bool alleys = v == 1;
                var d = new double[n * n];
                var nx = new int[n * n];
                for (int i = 0; i < n * n; i++) { d[i] = Inf; nx[i] = -1; }
                for (int i = 0; i < n; i++) { d[i * n + i] = 0; nx[i * n + i] = i; }
                foreach (var e in gd.Edges)
                {
                    if (e.Alley && !alleys) continue;
                    if (e.Km < d[e.A * n + e.B]) { d[e.A * n + e.B] = e.Km; d[e.B * n + e.A] = e.Km; nx[e.A * n + e.B] = e.B; nx[e.B * n + e.A] = e.A; }
                }
                for (int k = 0; k < n; k++)
                    for (int i = 0; i < n; i++)
                    {
                        double dik = d[i * n + k];
                        if (dik >= Inf) continue;
                        for (int j = 0; j < n; j++)
                        {
                            double alt = dik + d[k * n + j];
                            if (alt < d[i * n + j]) { d[i * n + j] = alt; nx[i * n + j] = nx[i * n + k]; }
                        }
                    }
                dist[v] = d;
                next[v] = nx;
                // ferry km along every shortest path (walk the next pointers once, at build time)
                var fk = new double[n * n];
                if (HasFerry)
                    for (int i = 0; i < n; i++)
                        for (int j = 0; j < n; j++)
                        {
                            if (d[i * n + j] >= Inf) continue;
                            int cur = i, guard = 0;
                            double sum = 0;
                            while (cur != j && guard++ < n + 2)
                            {
                                int nn = nx[cur * n + j];
                                if (nn < 0) break;
                                if (ferry[cur * n + nn]) sum += SegKm(cur, nn);
                                cur = nn;
                            }
                            fk[i * n + j] = sum;
                        }
                ferryKm[v] = fk;
            }
        }

        double SegKm(int a, int b)
        {
            var na = Gd.Nodes[a]; var nb = Gd.Nodes[b];
            return Math.Sqrt((na.X - nb.X) * (na.X - nb.X) + (na.Y - nb.Y) * (na.Y - nb.Y)) * Gd.RoadFactor;
        }

        /// <summary>Is the segment a..b (adjacent nodes) a ferry crossing?</summary>
        public bool IsFerry(int a, int b) => a >= 0 && b >= 0 && a < n && b < n && ferry[a * n + b];

        /// <summary>Ferry km on the shortest path a..b (0 when the path has no crossing).</summary>
        public double FerryKm(int a, int b, bool alleys = true)
        {
            if (a < 0 || b < 0 || a >= n || b >= n) return 0;
            return ferryKm[alleys ? 1 : 0][a * n + b];
        }

        /// <summary>Road km (fuel is paid on these) on the shortest path a..b.</summary>
        public double RoadKm(int a, int b, bool alleys = true)
        {
            double km = Km(a, b, alleys);
            return km >= Inf ? Inf : km - FerryKm(a, b, alleys);
        }

        /// <summary>Road km from a to b (Inf when unreachable for this vehicle class).</summary>
        public double Km(int a, int b, bool alleys = true)
        {
            if (a < 0 || b < 0 || a >= n || b >= n) return Inf;
            return dist[alleys ? 1 : 0][a * n + b];
        }

        public bool Reachable(int a, int b, bool alleys) => Km(a, b, alleys) < Inf;

        /// <summary>Node list a..b inclusive (empty when unreachable).</summary>
        public List<int> Path(int a, int b, bool alleys = true)
        {
            var p = new List<int>();
            if (!Reachable(a, b, alleys)) return p;
            var nx = next[alleys ? 1 : 0];
            int cur = a;
            p.Add(cur);
            int guard = 0;
            while (cur != b && guard++ < n + 2)
            {
                cur = nx[cur * n + b];
                if (cur < 0) { p.Clear(); return p; }
                p.Add(cur);
            }
            return p;
        }

        /// <summary>Length of a node path in km (sum of straight segments × road factor = the edge lengths).</summary>
        public double PathKm(List<int> path)
        {
            double km = 0;
            for (int i = 1; i < path.Count; i++)
            {
                var a = Gd.Nodes[path[i - 1]];
                var b = Gd.Nodes[path[i]];
                km += Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y)) * Gd.RoadFactor;
            }
            return km;
        }

        /// <summary>Position along a path at km from its start (for the diorama's trucks).</summary>
        public (double x, double y, double heading) PointAt(List<int> path, double km)
        {
            if (path == null || path.Count == 0) { var dep = Gd.Nodes[Math.Max(0, Gd.DepotNode)]; return (dep.X, dep.Y, 0); }
            if (path.Count == 1) { var only = Gd.Nodes[path[0]]; return (only.X, only.Y, 0); }
            double left = Math.Max(0, km);
            for (int i = 1; i < path.Count; i++)
            {
                var a = Gd.Nodes[path[i - 1]];
                var b = Gd.Nodes[path[i]];
                double seg = Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y)) * Gd.RoadFactor;
                double h = Math.Atan2(b.X - a.X, b.Y - a.Y);
                if (left <= seg || i == path.Count - 1)
                {
                    double t = seg <= 0 ? 1 : Math.Min(1, left / seg);
                    return (a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, h);
                }
                left -= seg;
            }
            var last = Gd.Nodes[path[path.Count - 1]];
            return (last.X, last.Y, 0);
        }
    }
}
