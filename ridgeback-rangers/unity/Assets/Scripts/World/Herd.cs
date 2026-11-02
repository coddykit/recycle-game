// Ridgeback Rangers — the dinosaurs on the diorama. One instanced draw per species (its own material = the colour morph on
// the family's Meshy model; the procedural MeshKit.Dino is the fallback), no GameObjects. Each animal wanders inside its
// paddock: idle -> walk to a random spot -> idle; while eating (feeding show) it walks to the paddock's front and dips its
// head; an escaped animal stands on the path by the viewpoint. Young animals are drawn at 60 %. Low tier (fleet §10):
// ≤ 24 animals drawn, updated at the caller's 15 Hz, no bob.
using System.Collections.Generic;
using UnityEngine;
using RidgebackRangers.Sim;

namespace RidgebackRangers.World
{
    public sealed class Herd
    {
        sealed class Walker
        {
            public Vector3 Pos, Target;
            public float Yaw, Wait, Phase;
            public int Paddock = -99;
        }

        readonly IslandWorld world;
        readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
        readonly Dictionary<string, bool> meshy = new Dictionary<string, bool>();
        readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
        readonly Dictionary<string, List<Matrix4x4>> batches = new Dictionary<string, List<Matrix4x4>>();
        readonly Dictionary<int, Walker> walkers = new Dictionary<int, Walker>();
        readonly Dictionary<int, Vector3> positions = new Dictionary<int, Vector3>();
        readonly System.Random rng = new System.Random(5);
        bool low;
        public const float BaseLength = 9f;   // world units a scale-1 adult is long (larger than life: it must read at map zoom)

        public Herd(IslandWorld world, Material palette, bool low)
        {
            this.world = world;
            this.low = low;
            foreach (var sp in world.Sim.Gd.Species)
            {
                if (!meshes.ContainsKey(sp.Family))
                {
                    var fm = LoadFamily(sp.Family, low);
                    meshy[sp.Family] = fm != null;
                    meshes[sp.Family] = fm ?? MeshKit.Dino(sp.Family);
                }
                var tex = Resources.Load<Texture2D>("kit/dino_" + sp.Family + (low ? "_base_low" : "_base"));
                var m = new Material(palette) { enableInstancing = true, name = "dino_" + sp.Id };
                if (tex != null) { m.mainTexture = tex; if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex); }
                var tint = ColorUtility.TryParseHtmlString(sp.Tint, out var c) ? c : Color.white;
                if (tex != null) tint = Color.Lerp(Color.white, tint, 0.55f);   // morph the Meshy texture, keep its detail
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint); else m.color = tint;
                mats[sp.Id] = m;
                batches[sp.Id] = new List<Matrix4x4>(16);
            }
        }

        /// <summary>Meshy model of a family (Resources/kit/dino_&lt;family&gt;_lod1/2.fbx, normalised: 1 unit long, faces +Z).</summary>
        static Mesh LoadFamily(string family, bool low)
        {
            var go = Resources.Load<GameObject>("kit/dino_" + family + (low ? "_lod2" : "_lod1"));
            var mf = go != null ? go.GetComponentInChildren<MeshFilter>() : null;
            return mf != null ? mf.sharedMesh : null;
        }

        public void SetLow(bool l) { low = l; }

        public Vector3? AnimalPos(int id) => positions.TryGetValue(id, out var p) ? p : (Vector3?)null;

        Vector3 RandomSpot(int paddock)
        {
            var c = world.PaddockCenter(paddock);
            var half = IslandWorld.PaddockSize * 0.5f - new Vector2(4f, 4f);
            return c + new Vector3((float)(rng.NextDouble() * 2 - 1) * half.x, 0, (float)(rng.NextDouble() * 2 - 1) * half.y);
        }

        public void Tick(float dt)
        {
            var s = world.Sim.S;
            foreach (var b in batches.Values) b.Clear();
            positions.Clear();
            int drawn = 0, max = low ? 24 : 48;
            var escaped = new HashSet<int>();
            foreach (var p in s.Pins) if (p.Kind == PinKinds.Escape && p.Animal >= 0) escaped.Add(p.Animal);
            for (int i = 0; i < s.Animals.Count; i++)
            {
                var a = s.Animals[i];
                if (!walkers.TryGetValue(a.Id, out var w)) { w = new Walker { Phase = (float)rng.NextDouble() * 6f }; walkers[a.Id] = w; }
                bool esc = escaped.Contains(i);
                int pd = esc ? -2 : a.Paddock;
                if (pd == -1 || a.Health == Sim.Health.Off) continue;   // nursery / off exhibit: not on the map
                if (w.Paddock != pd)
                {
                    w.Paddock = pd;
                    w.Pos = esc ? EscapeSpot(a) : RandomSpot(pd);
                    w.Target = w.Pos;
                    w.Wait = (float)rng.NextDouble() * 3f;
                }
                bool eating = a.ShowUntil > s.Minute && !esc;
                if (w.Wait > 0) w.Wait -= dt;
                else
                {
                    var d = w.Target - w.Pos; d.y = 0;
                    float speed = (esc ? 3f : 2.2f) * (s.Phase == Phases.Live && !s.Paused ? 1f : 0.4f);
                    if (d.magnitude < 0.5f)
                    {
                        w.Wait = eating ? 4f : 2f + (float)rng.NextDouble() * 5f;
                        w.Target = esc ? EscapeSpot(a) + new Vector3((float)rng.NextDouble() * 6 - 3, 0, (float)rng.NextDouble() * 6 - 3)
                            : eating ? world.PaddockCenter(pd) + new Vector3(((float)rng.NextDouble() * 2 - 1) * 10f, 0, -IslandWorld.PaddockSize.y * 0.35f) : RandomSpot(pd);
                    }
                    else
                    {
                        w.Pos += d.normalized * Mathf.Min(d.magnitude, speed * dt);
                        w.Yaw = Mathf.LerpAngle(w.Yaw, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, Mathf.Min(1, dt * 4f));
                    }
                }
                positions[a.Id] = w.Pos;
                if (drawn >= max) continue;
                drawn++;
                var sp = world.Sim.SpeciesOf(a);
                float size = BaseLength * (float)sp.Scale * (a.Young ? 0.6f : 1f);
                float bob = low ? 0f : Mathf.Abs(Mathf.Sin(Time.time * 3f + w.Phase)) * 0.15f * (w.Wait > 0 ? 0.3f : 1f);
                float pitch = eating && w.Wait > 0 ? 8f : 0f;
                var scale = meshy[sp.Family] ? Vector3.one * size : Vector3.one * (size / 8f);
                batches[sp.Id].Add(Matrix4x4.TRS(w.Pos + Vector3.up * bob, Quaternion.Euler(pitch, w.Yaw, 0), scale));
            }
        }

        Vector3 EscapeSpot(AnimalState a)
        {
            var zone = a.Paddock >= 0 && a.Paddock < world.Sim.S.Paddocks.Count ? world.Sim.ZoneOf(world.Sim.S.Paddocks[a.Paddock]) : "cove";
            int v = world.Sim.Gd.ViewOf(zone);
            return (v >= 0 ? world.NodePos(v) : Vector3.zero) + new Vector3(-6f, 0, -8f);
        }

        public void Draw()
        {
            foreach (var kv in batches)
            {
                if (kv.Value.Count == 0) continue;
                var sp = world.Sim.Gd.Sp(kv.Key);
                Graphics.RenderMeshInstanced(new RenderParams(mats[kv.Key]) { shadowCastingMode = low ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On },
                    meshes[sp.Family], 0, kv.Value);
            }
        }

        /// <summary>Nearest drawn animal to a ground point within r; -1 = none.</summary>
        public int Nearest(Vector3 g, float r)
        {
            int best = -1; float bd = r * r;
            var s = world.Sim.S;
            for (int i = 0; i < s.Animals.Count; i++)
            {
                if (!positions.TryGetValue(s.Animals[i].Id, out var p)) continue;
                float d = (p - g).sqrMagnitude;
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }
    }
}
