// Charge Nurse — the people of the diorama (plan §8): patients (pale gown) and staff in role colours (nurses teal,
// doctors white coats, porters orange, cleaners purple, receptionists blue, technicians navy). Chunky canonical figures
// (the fleet lesson: Meshy figures read worse at ~30 px), one instanced draw per mesh, no GameObjects. Each figure walks
// toward where the sim says it is (waiting seat, triage desk, bay, ward bed — lying down — the X-ray room, the lounge)
// and bobs while it walks. Visible cap ~28 people on Low (patients beyond it are counted on the UI markers only), the
// caller ticks it at 15 Hz on Low.
using System.Collections.Generic;
using UnityEngine;
using ChargeNurse.Sim;

namespace ChargeNurse.World
{
    public sealed class Crowd
    {
        sealed class Fig { public Vector3 Pos; public bool Seen; public bool Init; }

        static readonly int[] StaffShirts = { Pal.Nurse, Pal.Doctor, Pal.Porter, Pal.Cleaner, Pal.Clerk, Pal.Tech };
        readonly HospitalWorld world;
        readonly Material mat;
        readonly Mesh patientMesh;
        readonly Mesh[] staffMeshes = new Mesh[StaffShirts.Length];
        readonly List<Matrix4x4> standing = new List<Matrix4x4>(64), lying = new List<Matrix4x4>(64);
        readonly List<Matrix4x4>[] staff = new List<Matrix4x4>[StaffShirts.Length];
        readonly Dictionary<int, Fig> figs = new Dictionary<int, Fig>();
        readonly List<int> drop = new List<int>();
        readonly int[] staffUsed = new int[StaffShirts.Length];
        int maxPatients, maxStaff, staffDrawn;
        float time;

        public int Visible { get; private set; }

        public Crowd(HospitalWorld world, Material mat, bool low)
        {
            this.world = world;
            this.mat = mat;
            patientMesh = MeshKit.Figure(Pal.Gown, Pal.Gown);
            for (int i = 0; i < StaffShirts.Length; i++)
            {
                staffMeshes[i] = MeshKit.Figure(StaffShirts[i], StaffShirts[i] == Pal.Doctor ? Pal.Trousers : StaffShirts[i]);
                staff[i] = new List<Matrix4x4>(16);
            }
            SetLow(low);
        }

        public void SetLow(bool low)
        {
            maxPatients = low ? 18 : 40;   // Low: ≤ 28 people in all (fleet §10, plan §8)
            maxStaff = low ? 10 : 20;
        }

        /// <summary>Where the figure of a patient stands now (the UI anchors the patient chip on it).</summary>
        public bool PatientPos(int id, out Vector3 p)
        {
            if (figs.TryGetValue(id, out var f) && f.Init) { p = f.Pos; return true; }
            p = default;
            return false;
        }

        public void Tick(Simulation sim, float dt)
        {
            time += dt;
            standing.Clear(); lying.Clear();
            staffDrawn = 0;
            for (int i = 0; i < staff.Length; i++) { staff[i].Clear(); staffUsed[i] = 0; }
            foreach (var f in figs.Values) f.Seen = false;
            int shown = 0, waitIdx = 0, regIdx = 0, loungeIdx = 0;
            var testIdx = new Dictionary<int, int>();
            foreach (var p in sim.S.Patients)
            {
                Vector3 target; bool lie = false;
                if (p.Bed >= 0 && p.Bed < sim.S.Beds.Count && p.Stage != Stage.Move) { target = world.BedPos(p.Bed); lie = true; }
                else if (p.Room >= 0 && p.Room < sim.S.Rooms.Count) { target = world.MachinePos(sim.S.Rooms[p.Room].Zone); lie = p.Stage == Stage.Surgery || p.Stage == Stage.Board; }
                else
                {
                    switch (p.Stage)
                    {
                        case Stage.Reg: case Stage.Registering: target = world.Spot("reception", regIdx++); break;
                        case Stage.Triaging: case Stage.Fast: target = world.Spot("triage", 0); break;
                        case Stage.Test:
                        {
                            int z = sim.ZoneOfPatient(p);
                            testIdx.TryGetValue(z, out int k);
                            testIdx[z] = k + 1;
                            target = world.QueueSpot(z, k);
                            break;
                        }
                        case Stage.Move: target = p.Dest >= 0 && p.Dest < sim.S.Beds.Count ? world.BedPos(p.Dest) : world.ZoneCenter(sim.ZoneOfPatient(p)); break;
                        case Stage.Lounge: case Stage.Discharge: target = world.Spot("lounge", loungeIdx++); break;
                        default: target = world.WaitSpot(waitIdx++); break;
                    }
                }
                if (shown >= maxPatients) continue;
                shown++;
                if (!figs.TryGetValue(p.Id, out var f)) { f = new Fig(); figs[p.Id] = f; }
                if (!f.Init) { f.Pos = p.Amb ? world.AmbulanceBay : world.Entrance; f.Init = true; }
                f.Seen = true;
                float d = Vector3.Distance(f.Pos, target);
                bool walking = d > 0.05f;
                f.Pos = Vector3.MoveTowards(f.Pos, target, 6f * dt);
                if (lie && !walking) lying.Add(Matrix4x4.TRS(f.Pos + new Vector3(0, 0.62f, -0.75f), Quaternion.Euler(90, 0, 0), Vector3.one));
                else
                {
                    var pos = f.Pos;
                    if (walking) pos.y = Mathf.Abs(Mathf.Sin(time * 12f + p.Id)) * 0.12f;
                    var dir = target - f.Pos;
                    var rot = walking && dir.sqrMagnitude > 0.01f ? Quaternion.LookRotation(new Vector3(dir.x, 0, dir.z)) : Quaternion.Euler(0, 180, 0);
                    standing.Add(Matrix4x4.TRS(pos, rot, Vector3.one * 1.4f));
                    // a porter walks beside a moving patient
                    if (p.Stage == Stage.Move) Staff(Simulation.TPorter, pos + rot * new Vector3(0.9f, 0, 0), rot);
                }
                // staff at work next to the patient
                if (p.Doc && lie) Staff(Simulation.TDoctor, f.Pos + new Vector3(0.95f, 0, 0), Quaternion.Euler(0, -90, 0));
                if (p.Stage == Stage.Discharge && lie) Staff(Simulation.TNurse, f.Pos + new Vector3(-0.95f, 0, 0), Quaternion.Euler(0, 90, 0));
                if (p.Stage == Stage.Triaging || p.Stage == Stage.Fast) Staff(Simulation.TNurse, world.Spot("triage", 1), Quaternion.Euler(0, 0, 0));
                if (p.Stage == Stage.Registering) Staff(Simulation.TClerk, world.Spot("reception", -1), Quaternion.Euler(0, 180, 0));
            }
            Visible = shown;
            // figures of patients that left
            drop.Clear();
            foreach (var kv in figs) if (!kv.Value.Seen) drop.Add(kv.Key);
            foreach (int id in drop) figs.Remove(id);
            // cleaners at dirty-bed work, technicians at their machines, idle staff at their stations
            for (int b = 0; b < sim.S.Beds.Count; b++)
                if (sim.S.Beds[b].State == BedState.Cleaning) Staff(Simulation.TCleaner, world.BedPos(b) + new Vector3(-0.95f, 0, 0), Quaternion.Euler(0, 90, 0));
            foreach (var r in sim.S.Rooms) if (r.Techs > 0 && r.Kind != "or") Staff(Simulation.TTech, world.MachinePos(r.Zone) + new Vector3(1.4f, 0, -0.6f), Quaternion.Euler(0, -90, 0));
            Idle(Simulation.TClerk, sim.Team(Teams.Clerk), "reception");
            Idle(Simulation.TNurse, sim.Team(Teams.Nurse), "triage");
            Idle(Simulation.TDoctor, sim.Team(Teams.Doctor), "bays_c");
            Idle(Simulation.TPorter, sim.Team(Teams.Porter), "hall_c2");
            Idle(Simulation.TCleaner, sim.Team(Teams.Cleaner), "obs");
        }

        void Staff(int team, Vector3 at, Quaternion rot)
        {
            if (staffDrawn >= maxStaff) return;
            staff[team].Add(Matrix4x4.TRS(at, rot, Vector3.one * 1.4f));
            staffUsed[team]++;
            staffDrawn++;
        }

        void Idle(int team, int count, string zone)
        {
            for (int k = staffUsed[team]; k < count && k < 4; k++) Staff(team, world.Spot(zone, 10 + k), Quaternion.Euler(0, 180, 0));
        }

        public void Draw()
        {
            var rp = new RenderParams(mat);
            if (standing.Count > 0) Graphics.RenderMeshInstanced(rp, patientMesh, 0, standing);
            if (lying.Count > 0) Graphics.RenderMeshInstanced(rp, patientMesh, 0, lying);
            for (int i = 0; i < staff.Length; i++) if (staff[i].Count > 0) Graphics.RenderMeshInstanced(rp, staffMeshes[i], 0, staff[i]);
        }
    }
}
