// Docket Day — the people of the diorama (plan §8): staff at their desks (a little bob while they work, gone while out
// sick, on the courthouse steps while in court), clients queuing at reception, and walkers (a client arriving or leaving,
// a lawyer crossing the street to court). Chunky canonical figures (the fleet lesson: Meshy figures read worse at
// ~25 px), one instanced draw per shirt colour, no GameObjects. Low: ≤ 30 people and ≤ 50 % walkers, updated at 15 Hz by
// the caller; no per-frame allocation (lists are reused).
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
            public int Mesh;
        }

        /// <summary>Mesh slots: role shirts (with a folder), then client shirts (without).</summary>
        public const int Partner = 0, Associate = 1, Paralegal = 2, Intern = 3, Receptionist = 4, Investigator = 5, Clerk = 6, Suit = 7, ClientA = 8, ClientB = 9, ClientC = 10;
        static readonly int[] Shirts = { Pal.Brand, Pal.ShirtA, Pal.ShirtD, Pal.ShirtE, Pal.ShirtB, Pal.ShirtC, Pal.Teal, Pal.Dark, Pal.Sand, Pal.WallBrick, Pal.ShirtC };
        readonly Material mat;
        readonly Mesh[] meshes = new Mesh[Shirts.Length];
        readonly List<Matrix4x4>[] batches = new List<Matrix4x4>[Shirts.Length];
        readonly List<Walker> walkers = new List<Walker>();
        readonly List<(Vector3 pos, float yaw, int mesh, bool busy)> standing = new List<(Vector3, float, int, bool)>();
        int maxWalkers, maxPeople;
        float time;

        public int Count => walkers.Count + standing.Count;

        public Crowd(Material mat, bool low)
        {
            this.mat = mat;
            for (int i = 0; i < Shirts.Length; i++) { meshes[i] = MeshKit.Figure(Shirts[i], i < ClientA); batches[i] = new List<Matrix4x4>(32); }
            SetLow(low);
        }

        public void SetLow(bool low)
        {
            maxWalkers = low ? 6 : 12;    // Low: half the walkers (fleet §10)
            maxPeople = low ? 30 : 60;    // plan §8: people cap 30 on Low, 60 on Medium
            while (walkers.Count > maxWalkers) walkers.RemoveAt(0);
        }

        public static int RoleMesh(string role, bool outfits)
        {
            switch (role)
            {
                case "partner": return outfits ? Suit : Partner;
                case "associate": return outfits ? Suit : Associate;
                case "paralegal": return Paralegal;
                case "intern": return Intern;
                case "receptionist": return Receptionist;
                case "investigator": return Investigator;
                case "clerk": return Clerk;
            }
            return Intern;
        }

        /// <summary>The standing set (staff + queue), rebuilt by the world when the sim changed (not every frame).</summary>
        public void ClearStanding() => standing.Clear();
        public void Stand(Vector3 pos, float yaw, int mesh, bool busy) { if (standing.Count < maxPeople) standing.Add((pos, yaw, mesh, busy)); }

        public void Walk(Vector3 from, Vector3 to, int mesh)
        {
            if (walkers.Count >= maxWalkers) walkers.RemoveAt(0);
            walkers.Add(new Walker { From = from, To = to, T = 0, Speed = 0.35f, Mesh = Mathf.Clamp(mesh, 0, Shirts.Length - 1) });
        }

        public void Tick(float dt)
        {
            time += dt;
            for (int i = walkers.Count - 1; i >= 0; i--)
            {
                var w = walkers[i];
                w.T += w.Speed * dt;
                if (w.T >= 1f) { walkers.RemoveAt(i); continue; }
                walkers[i] = w;
            }
            for (int s = 0; s < batches.Length; s++) batches[s].Clear();
            for (int i = 0; i < standing.Count; i++)
            {
                var p = standing[i];
                var pos = p.pos;
                if (p.busy) pos.y += Mathf.Abs(Mathf.Sin(time * 5f + i)) * 0.12f;   // working: a small bob
                batches[p.mesh].Add(Matrix4x4.TRS(pos, Quaternion.Euler(0, p.yaw, 0), Vector3.one));
            }
            foreach (var w in walkers)
            {
                var p = Vector3.Lerp(w.From, w.To, w.T);
                p.y += Mathf.Abs(Mathf.Sin(w.T * 40f)) * 0.2f;
                var dir = w.To - w.From;
                var rot = dir.sqrMagnitude > 0.01f ? Quaternion.LookRotation(dir) : Quaternion.identity;
                batches[w.Mesh].Add(Matrix4x4.TRS(p, rot, Vector3.one));
            }
        }

        public void Draw()
        {
            for (int s = 0; s < batches.Length; s++)
                if (batches[s].Count > 0) Graphics.RenderMeshInstanced(new RenderParams(mat), meshes[s], 0, batches[s]);
        }
    }
}
