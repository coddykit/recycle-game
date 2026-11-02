// Pour & Roast — the people in the diorama: customers queueing at the counter, waiting at the pickup shelf, leaving with
// their cup (or leaving angry), and the baristas / cashier / runner / roaster assistant at their stations. Chunky canonical
// figures (fleet lesson: Meshy figures read worse at ~25 px), one instanced draw per colour, no GameObjects.
// Low: ≤ 14 customers, updated at 15 Hz by the caller (design §8 Low budget).
using System.Collections.Generic;
using UnityEngine;
using PourAndRoast.Sim;

namespace PourAndRoast.World
{
    public sealed class Crowd
    {
        enum Mode { Queue, Leave, Angry, Balk }

        struct Person
        {
            public int Ticket, Shirt;
            public Vector3 Pos, BalkTo;
            public Mode Mode;
            public float T;
        }

        static readonly int[] Shirts = { Pal.ShirtA, Pal.ShirtB, Pal.ShirtC, Pal.ShirtD, Pal.ShirtE, Pal.Warning };
        const int Angry = 5;
        const float Walk = 1.5f;
        readonly CafeWorld world;
        readonly Material mat;
        readonly Mesh[] custMeshes = new Mesh[Shirts.Length];
        readonly List<Matrix4x4>[] custBatches = new List<Matrix4x4>[Shirts.Length];
        // staff: barista, cashier, runner, roaster, helper
        readonly Mesh[] staffMeshes = new Mesh[5];
        readonly List<Matrix4x4>[] staffBatches = new List<Matrix4x4>[5];
        readonly List<Person> people = new List<Person>();
        readonly List<Vector3> staffPos = new List<Vector3>();
        readonly List<int> staffIds = new List<int>();
        readonly List<int> queueOrder = new List<int>();
        int max, apron = Pal.Apron;
        float clock;

        public int Count => people.Count;

        public Crowd(CafeWorld world, Material mat, bool low)
        {
            this.world = world;
            this.mat = mat;
            for (int i = 0; i < Shirts.Length; i++) { custMeshes[i] = MeshKit.Figure(Shirts[i], -1, i % 2 == 0 ? Pal.SkinTone : Pal.SkinTone2); custBatches[i] = new List<Matrix4x4>(32); }
            for (int i = 0; i < staffBatches.Length; i++) staffBatches[i] = new List<Matrix4x4>(8);
            BuildStaffMeshes();
            SetLow(low);
        }

        void BuildStaffMeshes()
        {
            staffMeshes[0] = MeshKit.Figure(Pal.Milk, apron);
            staffMeshes[1] = MeshKit.Figure(Pal.Teal, apron, Pal.SkinTone2);
            staffMeshes[2] = MeshKit.Figure(Pal.ShirtE, apron);
            staffMeshes[3] = MeshKit.Figure(Pal.Coffee, Pal.Sack, Pal.SkinTone2);
            staffMeshes[4] = MeshKit.Figure(Pal.Gold, apron);
        }

        public void SetApron(int swatch)
        {
            if (swatch == apron) return;
            apron = swatch;
            BuildStaffMeshes();
        }

        public void SetLow(bool low)
        {
            max = low ? 14 : 30;   // Low: half the crowd (fleet §10, design §8)
            while (people.Count > max) people.RemoveAt(0);
        }

        public void Clear() => people.Clear();

        /// <summary>A walk-in placed an order (pre-orders appear when they pick up).</summary>
        public void Arrive(int ticket, int cust, bool pre)
        {
            if (pre || people.Count >= max) return;
            people.Add(new Person { Ticket = ticket, Shirt = Mathf.Clamp(cust, 0, Shirts.Length - 2), Pos = world.DoorPos, Mode = Mode.Queue });
        }

        public void Balk(int cust)
        {
            if (people.Count >= max) return;
            people.Add(new Person { Ticket = -1, Shirt = Mathf.Clamp(cust, 0, Shirts.Length - 2), Pos = world.DoorPos, Mode = Mode.Balk, BalkTo = world.QueuePos(6) });
        }

        public void Leave(int ticket, bool happy)
        {
            for (int i = 0; i < people.Count; i++)
            {
                if (people[i].Ticket != ticket) continue;
                var p = people[i];
                p.Mode = happy ? Mode.Leave : Mode.Angry;
                if (!happy) p.Shirt = Angry;
                people[i] = p;
                return;
            }
            if (happy && people.Count < max)   // a pre-order customer picks up at the shelf
                people.Add(new Person { Ticket = ticket, Shirt = 2, Pos = world.WaitPos(0), Mode = Mode.Leave });
        }

        static Vector3 Toward(Vector3 from, Vector3 to, float d)
        {
            var delta = to - from;
            float m = delta.magnitude;
            return m <= d || m < 1e-4f ? to : from + delta / m * d;
        }

        public void Tick(float dt)
        {
            var sim = world.Sim;
            clock += dt;
            // queue slots: open walk-in tickets in arrival order, ready ones wait at the shelf
            queueOrder.Clear();
            int ready = 0;
            for (int i = people.Count - 1; i >= 0; i--)
            {
                var p = people[i];
                float step = Walk * dt;
                switch (p.Mode)
                {
                    case Mode.Queue:
                    {
                        var t = sim.Ticket(p.Ticket);
                        if (t == null) { p.Mode = Mode.Leave; break; }
                        Vector3 target;
                        if (t.Ready) target = world.WaitPos(ready++);
                        else { target = world.QueuePos(queueOrder.Count); queueOrder.Add(p.Ticket); }
                        p.Pos = Toward(p.Pos, target, step);
                        break;
                    }
                    case Mode.Leave:
                    case Mode.Angry:
                        p.Pos = Toward(p.Pos, world.ExitPos, step * (p.Mode == Mode.Angry ? 1.3f : 1f));
                        if ((p.Pos - world.ExitPos).sqrMagnitude < 0.01f) { people.RemoveAt(i); continue; }
                        break;
                    case Mode.Balk:
                        p.T += dt;
                        if (p.T < 2.5f) p.Pos = Toward(p.Pos, p.BalkTo, step);
                        else { p.Shirt = Angry; p.Pos = Toward(p.Pos, world.DoorPos, step * 1.3f); if ((p.Pos - world.DoorPos).sqrMagnitude < 0.01f) { people.RemoveAt(i); continue; } }
                        break;
                }
                people[i] = p;
            }
            for (int s = 0; s < custBatches.Length; s++) custBatches[s].Clear();
            var face = Quaternion.Euler(0, 0, 0);
            foreach (var p in people)
            {
                var pos = p.Pos;
                pos.y = p.Mode == Mode.Queue ? 0 : Mathf.Abs(Mathf.Sin(clock * 9f + p.Ticket)) * 0.05f;
                var rot = p.Mode == Mode.Queue ? face : Quaternion.Euler(0, 180, 0);
                custBatches[p.Shirt].Add(Matrix4x4.TRS(pos, rot, Vector3.one));
            }
            // staff at their stations
            for (int s = 0; s < staffBatches.Length; s++) staffBatches[s].Clear();
            staffPos.Clear(); staffIds.Clear();
            int esp = 0, milk = 0, any = 0;
            var back = Quaternion.Euler(0, 180, 0);
            foreach (var st in sim.StaffHere())
            {
                Vector3 p; int mesh;
                switch (st.Role)
                {
                    case Roles.Cashier: p = world.CashierPos; mesh = 1; break;
                    case Roles.Runner: p = world.StationPos("fridge") + new Vector3(0.3f, -world.StationPos("fridge").y, 0.7f); mesh = 2; break;
                    case Roles.Roaster: if (!world.HasStation("roaster")) continue; p = world.RoasterStaffPos; mesh = 3; break;
                    default:
                        p = world.BaristaPos(st.Station, st.Station == Stations.Espresso ? esp++ : st.Station == Stations.Milk ? milk++ : any++);
                        mesh = 0; break;
                }
                p.y = Mathf.Abs(Mathf.Sin(clock * 5f + st.Id * 1.7f)) * 0.03f;
                staffBatches[mesh].Add(Matrix4x4.TRS(p, back, Vector3.one));
                staffPos.Add(p); staffIds.Add(st.Id);
            }
            if (sim.HelperOn) staffBatches[4].Add(Matrix4x4.TRS(world.BaristaPos(Stations.Any, any + 1), back, Vector3.one));
        }

        public void Draw()
        {
            for (int s = 0; s < custBatches.Length; s++)
                if (custBatches[s].Count > 0) Graphics.RenderMeshInstanced(new RenderParams(mat), custMeshes[s], 0, custBatches[s]);
            for (int s = 0; s < staffBatches.Length; s++)
                if (staffBatches[s].Count > 0) Graphics.RenderMeshInstanced(new RenderParams(mat), staffMeshes[s], 0, staffBatches[s]);
        }

        /// <summary>The staff id under a ray (at chest height), -1 = none.</summary>
        public int PickStaff(Ray ray)
        {
            if (Mathf.Abs(ray.direction.y) < 1e-4f) return -1;
            float dist = (1.0f - ray.origin.y) / ray.direction.y;
            if (dist < 0) return -1;
            var g = ray.origin + ray.direction * dist;
            int best = -1; float bd = 0.45f * 0.45f;
            for (int i = 0; i < staffPos.Count; i++)
            {
                float d = (new Vector2(g.x, g.z) - new Vector2(staffPos[i].x, staffPos[i].z)).sqrMagnitude;
                if (d < bd) { bd = d; best = staffIds[i]; }
            }
            return best;
        }
    }
}
