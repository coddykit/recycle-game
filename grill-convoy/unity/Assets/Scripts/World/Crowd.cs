// Grill Convoy — the crowd in front of the trucks (design §4 loop 2, §8). Chunky canonical figures (fleet lesson: Meshy
// figures read worse at ~25 px), one instanced draw per customer-type colour, no GameObjects:
//   * the LINE: one figure per waiting customer, snaking away from the serving hatch toward the camera — the queue length is
//     the game's main readout (a line wrapping the planter = the lunch rush);
//   * LEAVERS: a served customer walks off with a burger in hand; a customer who walked away (line too long, too slow,
//     sold out) leaves in a red shirt — the visible cost.
// Low: ≤ 16 figures in a line per truck (plan §8: "≤ 16 animated"), half the leavers, updated at 15 Hz by the caller.
using System.Collections.Generic;
using UnityEngine;
using GrillConvoy.Sim;

namespace GrillConvoy.World
{
    public sealed class Crowd
    {
        struct Walker
        {
            public Vector3 From, To;
            public float T, Speed;
            public int Mesh;
        }

        // customer types: office, fan, beach, tourist, regular (+ red = walked away)
        static readonly int[] Shirts = { Pal.ShirtA, Pal.ShirtB, Pal.ShirtC, Pal.ShirtD, Pal.ShirtE, Pal.Warning };
        readonly RegionWorld world;
        readonly Material mat;
        readonly Mesh[] meshes;          // shirts without burger, then shirts with burger
        readonly List<Matrix4x4>[] batches;
        readonly List<Walker> walkers = new List<Walker>();
        readonly System.Random rng = new System.Random(11);
        int maxLine, maxWalkers, skip;

        public int Count { get; private set; }

        public Crowd(RegionWorld world, Material mat, bool low)
        {
            this.world = world;
            this.mat = mat;
            meshes = new Mesh[Shirts.Length * 2];
            batches = new List<Matrix4x4>[meshes.Length];
            for (int i = 0; i < Shirts.Length; i++)
            {
                meshes[i] = MeshKit.Figure(Shirts[i], false);
                meshes[Shirts.Length + i] = MeshKit.Figure(Shirts[i], true);
            }
            for (int i = 0; i < batches.Length; i++) batches[i] = new List<Matrix4x4>(48);
            SetLow(low);
        }

        public void SetLow(bool low)
        {
            maxLine = low ? 16 : 40;
            maxWalkers = low ? 12 : 30;
            while (walkers.Count > maxWalkers) walkers.RemoveAt(walkers.Count - 1);
        }

        /// <summary>A customer leaves truck ti: served (with a burger) or walked away (red).</summary>
        public void Leave(int ti, int arch, bool served)
        {
            if (ti < 0) return;
            if (walkers.Count >= maxWalkers) { if ((++skip % 2) == 0) return; walkers.RemoveAt(0); }
            var from = world.HatchWorld(ti);
            float a = (float)(rng.NextDouble() * Mathf.PI) + Mathf.PI;   // away from the truck, toward the camera half
            var to = from + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (30f + (float)rng.NextDouble() * 18f);
            int shirt = served ? Mathf.Clamp(arch, 0, Shirts.Length - 2) : Shirts.Length - 1;
            walkers.Add(new Walker { From = from, To = to, T = 0, Speed = 0.25f + (float)rng.NextDouble() * 0.1f, Mesh = served ? Shirts.Length + shirt : shirt });
        }

        public void Tick(float dt, Simulation sim)
        {
            for (int s = 0; s < batches.Length; s++) batches[s].Clear();
            Count = 0;
            // the lines
            for (int ti = 0; ti < sim.S.Trucks.Count; ti++)
            {
                var t = sim.S.Trucks[ti];
                if (!t.Open) continue;
                var hatch = world.HatchWorld(ti);
                int n = Mathf.Min(t.Queue.Count, maxLine);
                for (int i = 0; i < n; i++)
                {
                    int row = i / 6, col = i % 6;
                    float dir = row % 2 == 0 ? 1f : -1f;   // snake
                    var p = hatch + new Vector3((col - 2.5f) * 2.4f * dir + 0.3f * Mathf.Sin(Time.time * 2f + i), 0, -3.5f - row * 2.8f);
                    var rot = Quaternion.LookRotation(hatch - p + Vector3.forward * 0.01f);
                    int arch = Mathf.Clamp(t.Queue[i].Arch, 0, Shirts.Length - 2);
                    batches[arch].Add(Matrix4x4.TRS(p, rot, Vector3.one * 1.6f));
                    Count++;
                }
            }
            // the leavers
            for (int i = walkers.Count - 1; i >= 0; i--)
            {
                var w = walkers[i];
                w.T += w.Speed * dt;
                if (w.T >= 1f) { walkers.RemoveAt(i); continue; }
                walkers[i] = w;
                var p = Vector3.Lerp(w.From, w.To, w.T);
                p.y = Mathf.Abs(Mathf.Sin(w.T * 40f)) * 0.25f;   // a little bob = walking
                var dir = w.To - w.From;
                batches[w.Mesh].Add(Matrix4x4.TRS(p, dir.sqrMagnitude > 0.01f ? Quaternion.LookRotation(dir) : Quaternion.identity, Vector3.one * 1.6f));
                Count++;
            }
        }

        public void Draw()
        {
            for (int s = 0; s < batches.Length; s++)
                if (batches[s].Count > 0) Graphics.RenderMeshInstanced(new RenderParams(mat), meshes[s], 0, batches[s]);
        }
    }
}
