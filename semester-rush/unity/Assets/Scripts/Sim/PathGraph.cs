// Semester Rush sim core — engine independent. The campus walking graph: all-pairs shortest paths (Floyd–Warshall; the map
// has ~25 nodes) over the edges that are open right now (paths of unlocked zones, opened gates, the shuttle once
// researched). Weights are free-flow walking minutes. Rebuilt only when the topology changes (gate, zone, shuttle), never
// per frame.
using System;
using System.Collections.Generic;

namespace SemesterRush.Sim
{
    public sealed class PathGraph
    {
        public const double Inf = 1e18;
        readonly int n;
        readonly double[] dist;
        readonly int[] next;
        readonly int[] edgeOf;     // n×n -> edge index of the direct open edge, -1
        public readonly bool[] Open;

        /// <summary>open(edge) decides which edges exist; minutes(edge) is the free-flow walking time of one edge.</summary>
        public PathGraph(GameData gd, Func<EdgeDef, bool> open, Func<EdgeDef, double> minutes)
        {
            n = gd.Nodes.Count;
            dist = new double[n * n];
            next = new int[n * n];
            edgeOf = new int[n * n];
            Open = new bool[gd.Edges.Count];
            for (int i = 0; i < n * n; i++) { dist[i] = Inf; next[i] = -1; edgeOf[i] = -1; }
            for (int i = 0; i < n; i++) { dist[i * n + i] = 0; next[i * n + i] = i; }
            foreach (var e in gd.Edges)
            {
                if (!open(e)) continue;
                Open[e.Index] = true;
                double w = Math.Max(0.01, minutes(e));
                if (w < dist[e.A * n + e.B])
                {
                    dist[e.A * n + e.B] = dist[e.B * n + e.A] = w;
                    next[e.A * n + e.B] = e.B; next[e.B * n + e.A] = e.A;
                    edgeOf[e.A * n + e.B] = edgeOf[e.B * n + e.A] = e.Index;
                }
            }
            for (int k = 0; k < n; k++)
                for (int i = 0; i < n; i++)
                {
                    double dik = dist[i * n + k];
                    if (dik >= Inf) continue;
                    for (int j = 0; j < n; j++)
                    {
                        double alt = dik + dist[k * n + j];
                        if (alt < dist[i * n + j] - 1e-9) { dist[i * n + j] = alt; next[i * n + j] = next[i * n + k]; }
                    }
                }
        }

        /// <summary>Free-flow walking minutes a -> b (Inf when unreachable).</summary>
        public double Minutes(int a, int b) => a < 0 || b < 0 || a >= n || b >= n ? Inf : dist[a * n + b];
        public bool Reachable(int a, int b) => Minutes(a, b) < Inf;
        public int EdgeBetween(int a, int b) => a < 0 || b < 0 || a >= n || b >= n ? -1 : edgeOf[a * n + b];

        /// <summary>Node list a..b inclusive (empty when unreachable).</summary>
        public List<int> Path(int a, int b)
        {
            var p = new List<int>();
            if (!Reachable(a, b)) return p;
            int cur = a;
            p.Add(cur);
            int guard = 0;
            while (cur != b && guard++ < n + 2)
            {
                cur = next[cur * n + b];
                if (cur < 0) { p.Clear(); return p; }
                p.Add(cur);
            }
            return p;
        }

        /// <summary>The edges along a node path.</summary>
        public List<int> Edges(List<int> path)
        {
            var o = new List<int>();
            for (int i = 1; i < path.Count; i++) { int e = EdgeBetween(path[i - 1], path[i]); if (e >= 0) o.Add(e); }
            return o;
        }
    }
}
