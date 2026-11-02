// Docket Day — citizens walking to return points with bags (design §4 step 2, §8). Chunky canonical figures (the fleet
// lesson: Meshy figures read worse at ~25 px), one instanced draw per archetype colour, no GameObjects. A deposit event
// spawns a walker from a nearby street spot to the machine and back; a "turned away" walker turns red-capped and walks
// off without stopping — the visible cost of a full machine. Low: ≤ 50 % walkers, updated at 15 Hz by the caller.
using System.Collections.Generic;
using UnityEngine;

namespace DocketDay.World
{
    public sealed class Crowd
    {
        struct Walker
        {
            public Vector3 From, To;
            public float T, Speed;
            public int Shirt;
            public bool Turned, Back;
        }

        static readonly int[] Shirts = { Pal.ShirtA, Pal.ShirtB, Pal.ShirtC, Pal.ShirtD, Pal.ShirtE, Pal.Warning };
        readonly RegionWorld world;
        readonly Material mat;
        readonly Mesh[] meshes = new Mesh[Shirts.Length];
        readonly List<Matrix4x4>[] batches = new List<Matrix4x4>[Shirts.Length];
        readonly List<Walker> walkers = new List<Walker>();
        readonly System.Random rng = new System.Random(11);
        int max;
        int skip;

        public int Count => walkers.Count;

        public Crowd(RegionWorld world, Material mat, bool low)
        {
            this.world = world;
            this.mat = mat;
            for (int i = 0; i < Shirts.Length; i++) { meshes[i] = MeshKit.Figure(Shirts[i]); batches[i] = new List<Matrix4x4>(64); }
            SetLow(low);
        }

        public void SetLow(bool low)
        {
            max = low ? 24 : 48;   // Low: half the crowd (fleet §10)
            while (walkers.Count > max) walkers.RemoveAt(walkers.Count - 1);
        }

        /// <summary>archetype -1 = HoReCa staff bag drop (no walker).</summary>
        public void Spawn(int point, int archetype, bool turned)
        {
            if (archetype < 0 || point < 0 || point >= world.Sim.S.Points.Count) return;
            if (walkers.Count >= max) { if (!turned && (++skip % 2) == 0) return; walkers.RemoveAt(0); }
            var to = world.PointPos(point) + new Vector3(0, 0, -3f);
            float a = (float)(rng.NextDouble() * Mathf.PI * 2);
            var from = to + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (28f + (float)rng.NextDouble() * 18f);
            walkers.Add(new Walker
            {
                From = from, To = to, T = 0, Speed = 0.22f + (float)rng.NextDouble() * 0.1f,
                Shirt = turned ? Shirts.Length - 1 : Mathf.Clamp(archetype, 0, Shirts.Length - 2), Turned = turned,
            });
        }

        public void Tick(float dt)
        {
            for (int i = walkers.Count - 1; i >= 0; i--)
            {
                var w = walkers[i];
                w.T += w.Speed * dt * (w.Turned && !w.Back ? 1.6f : 1f);
                if (w.T >= 1f)
                {
                    if (w.Back) { walkers.RemoveAt(i); continue; }
                    w.Back = true;
                    w.T = 0f;
                    (w.From, w.To) = (w.To, w.From);
                }
                walkers[i] = w;
            }
            for (int s = 0; s < batches.Length; s++) batches[s].Clear();
            foreach (var w in walkers)
            {
                float t = w.Turned && !w.Back ? Mathf.Min(w.T, 0.75f) : w.T;
                var p = Vector3.Lerp(w.From, w.To, t);
                p.y = Mathf.Abs(Mathf.Sin(w.T * 40f)) * 0.25f;   // a little bob = walking
                var dir = w.To - w.From;
                var rot = dir.sqrMagnitude > 0.01f ? Quaternion.LookRotation(dir) : Quaternion.identity;
                batches[w.Shirt].Add(Matrix4x4.TRS(p, rot, Vector3.one * 1.6f));
            }
        }

        public void Draw()
        {
            for (int s = 0; s < batches.Length; s++)
                if (batches[s].Count > 0) Graphics.RenderMeshInstanced(new RenderParams(mat), meshes[s], 0, batches[s]);
        }
    }
}
