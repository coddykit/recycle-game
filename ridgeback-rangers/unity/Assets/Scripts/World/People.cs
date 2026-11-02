// Ridgeback Rangers — people on the diorama: rangers (khaki hat figures, one per team, at their sim position on the service
// road) and visitors (walking in from the gate, waiting in the tram queue). Chunky canonical figures (fleet lesson: Meshy
// figures read worse at ~25 px), one instanced draw per colour, no GameObjects. Low: ≤ 50 % visitors, updated at the
// caller's 15 Hz.
using System.Collections.Generic;
using UnityEngine;
using RidgebackRangers.Sim;

namespace RidgebackRangers.World
{
    public sealed class People
    {
        struct Walker { public Vector3 From, To; public float T, Speed; public int Shirt; }

        static readonly int[] Shirts = { Pal.ShirtA, Pal.ShirtB, Pal.ShirtC, Pal.ShirtD, Pal.ShirtE };
        static readonly Dictionary<string, int> RoleShirt = new Dictionary<string, int>
        {
            { Roles.Feeder, Pal.Brand }, { Roles.Vet, Pal.PureWhite }, { Roles.Handler, Pal.Sunset }, { Roles.Tech, Pal.Yellow },
        };
        readonly IslandWorld world;
        readonly Material mat;
        readonly Mesh[] visitorMeshes = new Mesh[Shirts.Length];
        readonly Dictionary<string, Mesh> rangerMeshes = new Dictionary<string, Mesh>();
        readonly List<Matrix4x4>[] visitorBatches = new List<Matrix4x4>[Shirts.Length];
        readonly Dictionary<string, List<Matrix4x4>> rangerBatches = new Dictionary<string, List<Matrix4x4>>();
        readonly List<Walker> walkers = new List<Walker>();
        readonly System.Random rng = new System.Random(11);
        int maxWalkers, maxQueue;

        public People(IslandWorld world, Material mat, bool low)
        {
            this.world = world;
            this.mat = mat;
            for (int i = 0; i < Shirts.Length; i++) { visitorMeshes[i] = MeshKit.Figure(Shirts[i], false); visitorBatches[i] = new List<Matrix4x4>(32); }
            foreach (var kv in RoleShirt) { rangerMeshes[kv.Key] = MeshKit.Figure(kv.Value, true); rangerBatches[kv.Key] = new List<Matrix4x4>(8); }
            SetLow(low);
        }

        public void SetLow(bool low)
        {
            maxWalkers = low ? 12 : 30;   // Low: half the crowd (fleet §10)
            maxQueue = low ? 10 : 24;
            while (walkers.Count > maxWalkers) walkers.RemoveAt(walkers.Count - 1);
        }

        /// <summary>A group came through the gate: a few walkers stroll to the tram station.</summary>
        public void Arrive(int n)
        {
            for (int k = 0; k < Mathf.Min(n, 3); k++)
            {
                if (walkers.Count >= maxWalkers) walkers.RemoveAt(0);
                var gate = world.NodePos(world.Sim.Gd.GateNode);
                var st = world.NodePos(world.Sim.Gd.StationNode);
                walkers.Add(new Walker
                {
                    From = gate + new Vector3((float)rng.NextDouble() * 8 - 4, 0, 0), To = st + new Vector3(-10 + (float)rng.NextDouble() * 6, 0, -5),
                    Speed = 0.12f + (float)rng.NextDouble() * 0.05f, Shirt = rng.Next(Shirts.Length),
                });
            }
        }

        public void Tick(float dt)
        {
            for (int s = 0; s < visitorBatches.Length; s++) visitorBatches[s].Clear();
            foreach (var b in rangerBatches.Values) b.Clear();
            for (int i = walkers.Count - 1; i >= 0; i--)
            {
                var w = walkers[i];
                w.T += w.Speed * dt;
                if (w.T >= 1f) { walkers.RemoveAt(i); continue; }
                walkers[i] = w;
                var p = Vector3.Lerp(w.From, w.To, w.T);
                p.y = Mathf.Abs(Mathf.Sin(w.T * 60f)) * 0.2f;
                visitorBatches[w.Shirt].Add(Matrix4x4.TRS(p, Quaternion.LookRotation(w.To - w.From), Vector3.one * 1.6f));
            }
            // the tram queue: a short line at the station
            var sim = world.Sim;
            int q = Mathf.Min(maxQueue, sim.Queued);
            var st = world.NodePos(sim.Gd.StationNode);
            for (int k = 0; k < q; k++)
            {
                var p = st + new Vector3(-8f + (k % 8) * 2.2f, 0.6f, -6f - (k / 8) * 2.2f);
                visitorBatches[k % Shirts.Length].Add(Matrix4x4.TRS(p, Quaternion.identity, Vector3.one * 1.6f));
            }
            // rangers
            for (int i = 0; i < sim.S.Rangers.Count; i++)
            {
                var r = sim.S.Rangers[i];
                var (x, y, h) = sim.RangerPoint(r);
                var p = IslandWorld.ToWorld(x, y);
                bool home = r.Status == RangerStatus.Idle;
                if (home) p += new Vector3(-6f + (i % 6) * 2.4f, 0, -7f - (i / 6) * 2.4f);   // lined up in front of the HQ
                else p += new Vector3((i % 3 - 1) * 1.6f, 0, (i % 2) * 1.6f);
                float bob = r.Status == RangerStatus.Travel || r.Status == RangerStatus.Back ? Mathf.Abs(Mathf.Sin(Time.time * 8f + i)) * 0.25f : 0f;
                var rot = home ? Quaternion.identity : Quaternion.Euler(0, (float)(h * Mathf.Rad2Deg), 0);
                if (rangerBatches.TryGetValue(r.Role, out var list)) list.Add(Matrix4x4.TRS(p + Vector3.up * bob, rot, Vector3.one * 1.9f));
            }
        }

        public void Draw()
        {
            var rp = new RenderParams(mat);
            for (int s = 0; s < visitorBatches.Length; s++)
                if (visitorBatches[s].Count > 0) Graphics.RenderMeshInstanced(rp, visitorMeshes[s], 0, visitorBatches[s]);
            foreach (var kv in rangerBatches)
                if (kv.Value.Count > 0) Graphics.RenderMeshInstanced(rp, rangerMeshes[kv.Key], 0, kv.Value);
        }
    }
}
