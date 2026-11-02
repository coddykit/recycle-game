// Semester Rush — the bell-change crowd + the path heat ribbon (plan section 8, the signature view). Drawn from the sim's
// cached BellResult (no agents): every trip is a short train of students along its path, its lead walkers chunky canonical
// figures in the faculty colour (the fleet lesson: Meshy figures read worse at ~25 px), the rest instanced dots; late
// trains turn red-dotted. Each path under load gets a ribbon: green (fine), yellow (busy), red (jam). One instanced draw
// per colour, no GameObjects. Low: ≤ 50 % walkers and dots (15 Hz by the caller), max 15 animated figures.
using System.Collections.Generic;
using UnityEngine;
using SemesterRush.Sim;

namespace SemesterRush.World
{
    public sealed class Crowd
    {
        readonly CampusWorld world;
        readonly Material mat;
        readonly Mesh[] figures = new Mesh[Pal.Faculty.Length];
        readonly List<Matrix4x4>[] figureBatches = new List<Matrix4x4>[Pal.Faculty.Length];
        readonly Mesh dotOk, dotLate;
        readonly List<Matrix4x4> dotsOk = new List<Matrix4x4>(256), dotsLate = new List<Matrix4x4>(128);
        readonly Mesh[] ribbons = new Mesh[3];
        readonly List<Matrix4x4>[] ribbonBatches = { new List<Matrix4x4>(32), new List<Matrix4x4>(32), new List<Matrix4x4>(32) };
        bool low;
        public int Walkers { get; private set; }

        public Crowd(CampusWorld world, Material mat, bool low)
        {
            this.world = world;
            this.mat = mat;
            for (int i = 0; i < figures.Length; i++) { figures[i] = MeshKit.Figure(Pal.Faculty[i]); figureBatches[i] = new List<Matrix4x4>(16); }
            dotOk = MeshKit.Dot(Pal.White, 0.9f);
            dotLate = MeshKit.Dot(Pal.Warning, 0.9f);
            ribbons[0] = MeshKit.Ribbon(Pal.HeatOk);
            ribbons[1] = MeshKit.Ribbon(Pal.HeatBusy);
            ribbons[2] = MeshKit.Ribbon(Pal.HeatJam);
            SetLow(low);
        }

        public void SetLow(bool l) => low = l;

        public void Tick()
        {
            for (int s = 0; s < figureBatches.Length; s++) figureBatches[s].Clear();
            dotsOk.Clear(); dotsLate.Clear();
            foreach (var r in ribbonBatches) r.Clear();
            Walkers = 0;
            var sim = world.Sim;
            if (sim == null || !sim.InBell) return;
            var bell = sim.Bell;
            if (bell == null) return;
            var gd = sim.Gd;
            // heat ribbons
            for (int e = 0; e < bell.Heat.Length && e < gd.Edges.Count; e++)
            {
                double h = bell.Heat[e];
                if (h < 0.05) continue;
                int level = h <= 0.7 ? 0 : h <= 1.0 ? 1 : 2;
                Vector3 a = world.NodePos(gd.Edges[e].A), b = world.NodePos(gd.Edges[e].B);
                var d = b - a;
                float len = d.magnitude;
                if (len < 0.1f) continue;
                float width = 2.5f + Mathf.Min(4f, (float)h * 3f);
                ribbonBatches[level].Add(Matrix4x4.TRS(a, Quaternion.LookRotation(d / len), new Vector3(width, 1, len)));
            }
            // walkers
            int slot = bell.Slot;
            double elapsed = sim.S.Minute - (gd.Slots[slot] - gd.BellMin);
            int maxFigures = low ? 15 : 30, figuresUsed = 0;
            float share = low ? 0.5f : 1f;
            foreach (var t in bell.Trips)
            {
                if (t.Path.Count < 2) continue;
                int dots = Mathf.Clamp(Mathf.RoundToInt(t.Count / 10f * share), 1, low ? 4 : 8);
                int fac = gd.Faculty(gd.Cohorts[t.Cohort].Faculty).Index % figures.Length;
                double total = System.Math.Max(0.5, t.Minutes);
                for (int k = 0; k < dots; k++)
                {
                    // the train spreads out: the last walkers are the slow ones (and the late ones)
                    double speedK = 1.0 + (dots <= 1 ? 0 : (double)k / (dots - 1)) * 0.4 - 0.2;
                    float f = Mathf.Clamp01((float)(elapsed / (total * speedK)));
                    var p = world.PathPoint(t.Path, f) + new Vector3((k % 3 - 1) * 0.9f, 0, (k / 3 % 2) * 0.9f);
                    bool late = t.Late > 0 && (double)k / dots >= 1 - t.Late;
                    Vector3 dir = world.PathDir(t.Path, f);
                    if (k == 0 && figuresUsed < maxFigures)
                    {
                        figuresUsed++;
                        float bob = Mathf.Abs(Mathf.Sin((float)elapsed * 6f + t.Cohort)) * 0.25f;
                        figureBatches[fac].Add(Matrix4x4.TRS(p + Vector3.up * bob, dir.sqrMagnitude > 0 ? Quaternion.LookRotation(dir) : Quaternion.identity, Vector3.one * 1.3f));
                    }
                    else (late ? dotsLate : dotsOk).Add(Matrix4x4.TRS(p, Quaternion.identity, Vector3.one));
                    Walkers++;
                }
            }
        }

        public void Draw()
        {
            var rp = new RenderParams(mat);
            for (int i = 0; i < ribbons.Length; i++) if (ribbonBatches[i].Count > 0) Graphics.RenderMeshInstanced(rp, ribbons[i], 0, ribbonBatches[i]);
            for (int s = 0; s < figureBatches.Length; s++) if (figureBatches[s].Count > 0) Graphics.RenderMeshInstanced(rp, figures[s], 0, figureBatches[s]);
            if (dotsOk.Count > 0) Graphics.RenderMeshInstanced(rp, dotOk, 0, dotsOk);
            if (dotsLate.Count > 0) Graphics.RenderMeshInstanced(rp, dotLate, 0, dotsLate);
        }
    }
}
