// Charge Nurse sim core — the patient pipeline (plan §4): Arrive → Register → Triage → (Test) → Bay + doctor → home, or
// admitted: porter → ward bed → stay → discharge paperwork → home; the bed turns dirty → cleaner → ready. Teams are
// pools (plan §5: no individual micromanagement); a task holds one member until it is done. The player overrides any
// step (tag, send to a bay, discharge, clean) — the teams do the rest by the rules. Fatigue slows a team; the bottleneck
// lens names the stage that blocks the flow in one sentence.
using System;
using System.Collections.Generic;

namespace ChargeNurse.Sim
{
    public sealed class Bottleneck
    {
        public string Key = "smooth";   // smooth | discharge | dirty | beds | porters | or | doctors | bays | triage | tests | reception
        public string Team = "";        // the team that would help ("" = capacity: buy beds / bays / the OR)
        public int Zone = -1;           // where the red line goes
        public int Count;
    }

    public sealed partial class Simulation
    {
        public const int TNurse = 0, TDoctor = 1, TPorter = 2, TCleaner = 3, TClerk = 4, TTech = 5;
        readonly int[] busy = new int[6];

        // ------------------------------------------------------------------------------------------------ teams

        /// <summary>Members of a team working now (sick call −1, rewarded float +1, the night team a share).</summary>
        public int Avail(int t)
        {
            int n;
            if (t == TTech) { n = 0; foreach (var r in S.Rooms) if (r.Kind != "or" && r.Down <= 0) n += r.Techs; return n; }
            string id = Teams.All[t];
            n = Team(id);
            if (S.FloatLeft > 0 && S.FloatTeam == id) n++;
            if (S.Surge && !night && (t == TNurse || t == TDoctor)) n++;
            if (S.SickTeam == id && n > 1) n--;
            if (night && n > 0) n = Math.Max(1, (int)Math.Floor(n * (Has("night_team") ? Gd.NightEffTeam : Gd.NightEff) + 1e-9));
            return n;
        }

        public int Busy(int t) => busy[t];
        bool Free(int t) => busy[t] < Avail(t);

        /// <summary>Speed factor of a team: fatigue and hospital-wide burnout slow it (never below half speed).</summary>
        public double Eff(int t)
        {
            double f = S.Fatigue.TryGetValue(Teams.All[t], out var v) ? v : 0;
            double e = 1 - Gd.FatigueMaxSlow * Math.Max(0, Math.Min(100, f)) / 100.0;
            if (S.Burnout >= Gd.BurnoutSlowAt) e -= Gd.BurnoutSlow;
            return Math.Max(0.5, e);
        }

        public double Accuracy => Has("triage_training") ? 0.95 : Gd.TriageAccuracy;

        void CountBusy()
        {
            Array.Clear(busy, 0, busy.Length);
            foreach (var p in S.Patients)
                switch (p.Stage)
                {
                    case Stage.Registering: busy[TClerk]++; break;
                    case Stage.Triaging: case Stage.Discharge: case Stage.Fast: busy[TNurse]++; break;
                    case Stage.Treat: case Stage.Surgery: if (p.Doc) busy[TDoctor]++; break;
                    case Stage.Move: busy[TPorter]++; break;
                    case Stage.Testing: busy[TTech]++; break;
                }
            foreach (var b in S.Beds) if (b.State == BedState.Cleaning) busy[TCleaner]++;
        }

        // ------------------------------------------------------------------------------------------------ the step

        void Flow(double dt)
        {
            CountBusy();
            Progress(dt);
            Waits(dt);
            AssignCleaners();
            AssignPorters();
            AssignNurses();
            AssignClerks();
            AssignTests();
            AssignBays();
            AssignDoctors();
        }

        void Progress(double dt)
        {
            for (int i = S.Patients.Count - 1; i >= 0; i--)
            {
                if (i >= S.Patients.Count) continue;
                var p = S.Patients[i];
                p.Total += dt;
                switch (p.Stage)
                {
                    case Stage.Registering:
                        if ((p.Left -= dt) <= 0) { busy[TClerk]--; p.Stage = Stage.Triage; }
                        break;
                    case Stage.Triaging:
                        if ((p.Left -= dt) <= 0) { busy[TNurse]--; AutoTag(p); AfterTriage(p); }
                        break;
                    case Stage.Testing:
                    {
                        var r = p.Room >= 0 && p.Room < S.Rooms.Count ? S.Rooms[p.Room] : null;
                        if (r != null && r.Down > 0) break;   // the machine is down: the scan waits
                        if ((p.Left -= dt) <= 0)
                        {
                            busy[TTech]--;
                            if (r != null) r.Patient = -1;
                            p.Room = -1;
                            p.Test = "";
                            p.Stage = Stage.Bay;
                            if (EmitCrowd) Emit("tested", p.Id);
                        }
                        break;
                    }
                    case Stage.Treat:
                        if (p.Doc && (p.Left -= dt) <= 0) { busy[TDoctor]--; p.Doc = false; AfterTreat(p); }
                        break;
                    case Stage.Fast:
                        if ((p.Left -= dt) <= 0) { busy[TNurse]--; Leave(p, true); }
                        break;
                    case Stage.Discharge:
                        if ((p.Left -= dt) <= 0) { busy[TNurse]--; Leave(p, true); }
                        break;
                    case Stage.Move:
                        if ((p.Left -= dt) <= 0) { busy[TPorter]--; ArriveAtDest(p); }
                        break;
                    case Stage.Surgery:
                    {
                        var r = p.Room >= 0 && p.Room < S.Rooms.Count ? S.Rooms[p.Room] : null;
                        if (!p.Doc || (r != null && r.Down > 0)) break;
                        if ((p.Left -= dt) <= 0)
                        {
                            busy[TDoctor]--;
                            p.Doc = false;
                            p.NeedsOr = false;
                            p.Ward = p.Sev >= Sev.Red && BedsIn("icu") > 0 ? "icu" : BedsIn("surgical") > 0 ? "surgical" : PickWard(p.Sev);
                            p.Stage = Stage.Board;   // recovering in the OR until a bed and a porter are free
                            Emit("surgery_done", p.Id);
                        }
                        break;
                    }
                    case Stage.Stay:
                        if ((p.Left -= dt) <= 0)
                        {
                            p.Left = 0;
                            p.Stage = Stage.WardReady;
                            if (Has("discharge_lounge") && Unlocked("town") && !p.Guided)
                            {
                                FreeBed(p);   // the lounge takes the paperwork wait: the bed is free for the next patient
                                p.Stage = Stage.Lounge;
                            }
                        }
                        break;
                }
            }
            foreach (var b in S.Beds)
                if (b.State == BedState.Cleaning && (b.Left -= dt) <= 0)
                {
                    b.Left = 0;
                    b.State = BedState.Clean;
                    b.Guided = false;
                    busy[TCleaner]--;
                }
        }

        void AutoTag(Patient p)
        {
            int tag = p.Sev;
            if (Rng.Frand() > Accuracy)
            {
                int d = Rng.Frand() < 0.5 ? -1 : 1;
                tag = Math.Max(0, Math.Min(Sev.Red, p.Sev + d));
            }
            p.Tag = tag;
        }

        void AfterTriage(Patient p)
        {
            p.Stage = p.Test != "" ? Stage.Test : Stage.Bay;
            if (EmitCrowd) Emit("triaged", p.Id, p.Tag);
        }

        void AfterTreat(Patient p)
        {
            if (p.NeedsOr || p.Admit) p.Stage = Stage.Board;
            else p.Stage = Stage.Ready;
            Emit("treated", p.Id, p.Sev, 0, p.Stage);
        }

        void ArriveAtDest(Patient p)
        {
            if (p.Dest <= -2)
            {
                int ri = -2 - p.Dest;
                p.Room = ri;
                p.Stage = Stage.Surgery;
                p.Left = Gd.OrMin / (Has("or_checklist") ? 1.25 : 1.0);
                p.Doc = false;
            }
            else if (p.Dest >= 0 && p.Dest < S.Beds.Count)
            {
                p.Bed = p.Dest;
                p.Stage = Stage.Stay;
                p.Left = Gd.StayMin[p.Sev] * (0.85 + 0.3 * Rng.Frand());
                S.Today.Admitted++;
                if (p.NoteLost) p.Left += Gd.NoteDelayMin;
            }
            p.Dest = -1;
            if (EmitCrowd) Emit("admitted", p.Id);
        }

        void FreeBed(Patient p)
        {
            if (p.Bed >= 0 && p.Bed < S.Beds.Count)
            {
                var b = S.Beds[p.Bed];
                if (b.Patient == p.Id) { b.Patient = -1; b.State = BedState.Dirty; b.Guided = p.Guided; }
            }
            p.Bed = -1;
            if (p.Room >= 0 && p.Room < S.Rooms.Count && S.Rooms[p.Room].Patient == p.Id) S.Rooms[p.Room].Patient = -1;
            p.Room = -1;
        }

        /// <summary>The patient leaves. treated = paid (case by true severity × reputation, ambulance bonus).</summary>
        void Leave(Patient p, bool treated, string why = "discharged")
        {
            FreeBed(p);
            if (p.Dest >= 0 && p.Dest < S.Beds.Count && S.Beds[p.Dest].Patient == p.Id) { S.Beds[p.Dest].Patient = -1; S.Beds[p.Dest].State = BedState.Dirty; }
            else if (p.Dest <= -2 && -2 - p.Dest < S.Rooms.Count && S.Rooms[-2 - p.Dest].Patient == p.Id) S.Rooms[-2 - p.Dest].Patient = -1;
            double fee = 0;
            if (treated)
            {
                fee = Math.Round(Gd.Pay[p.Sev] * PayMult * (p.Amb ? Gd.AmbulancePayMult : 1.0));
                S.Cash += fee;
                S.Today.Fees += fee;
                if (night) { S.Today.NightFees += fee; S.Today.NightTreated++; }
                S.Today.Treated++;
                S.CareerTreated++;
            }
            S.Patients.Remove(p);
            Emit(why, p.Id, p.Sev, fee);
        }

        // ------------------------------------------------------------------------------------------------ waits

        void Waits(double dt)
        {
            for (int i = S.Patients.Count - 1; i >= 0; i--)
            {
                if (i >= S.Patients.Count) continue;
                var p = S.Patients[i];
                if (p.Seen || p.Guided) continue;
                bool waiting = Stage.PreDoctor(p.Stage) || (p.Stage == Stage.Treat && !p.Doc);
                if (!waiting) continue;
                p.Wait += dt;
                if (S.Tutorial || night) continue;   // the guide and the night team are safe (fleet §9)
                if (!p.Worsened && p.Wait > Gd.SafeWait[p.Sev])
                {
                    p.Worsened = true;
                    if (p.Sev < Sev.Red) p.Sev++;
                    S.Today.Worsened++;
                    Emit("worsened", p.Id, p.Sev);
                }
                else if (p.Worsened && p.Sev >= Sev.Red && p.Wait > Gd.SafeWait[Sev.Red] * 3 + 45)
                {
                    S.Today.Transferred++;
                    Leave(p, false, "transfer");   // "transferred elsewhere" — never a death (plan §5 tone)
                    continue;
                }
                if (p.Sev <= Sev.Yellow && (p.Stage == Stage.Reg || p.Stage == Stage.Triage || p.Stage == Stage.Test || p.Stage == Stage.Bay)
                    && p.Wait > Gd.LwbsWait[p.Sev] * (Has("waiting_hall") ? 1.3 : 1.0))
                {
                    S.Today.Lwbs++;
                    Leave(p, false, "lwbs");
                }
            }
        }

        // ------------------------------------------------------------------------------------------------ assignment

        int Prio(Patient p)
        {
            int tag = Math.Max(0, p.Tag);
            if (tag == Sev.Red) return 100000 + (int)p.Wait;           // red always jumps the queue
            switch (S.Queue)
            {
                case QueueRules.Urgent: return tag * 10000 + (int)p.Wait;
                case QueueRules.Arrival: return 50000 - Math.Min(49999, p.Id);
                default: return tag * 45 + (int)p.Wait;
            }
        }

        readonly List<Patient> scratch = new List<Patient>();

        List<Patient> Queue(string stage, Func<Patient, bool> extra = null)
        {
            scratch.Clear();
            foreach (var p in S.Patients) if (p.Stage == stage && !p.Guided && (extra == null || extra(p))) scratch.Add(p);
            scratch.Sort((a, b) => { int c = Prio(b).CompareTo(Prio(a)); return c != 0 ? c : a.Id.CompareTo(b.Id); });
            return scratch;
        }

        void AssignCleaners()
        {
            for (int pass = 0; pass < 2 && Free(TCleaner); pass++)
                foreach (var b in S.Beds)
                {
                    if (!Free(TCleaner)) return;
                    if (b.State != BedState.Dirty || b.Guided || b.Bay != (pass == 0)) continue;   // bays first: the ED flow
                    b.State = BedState.Cleaning;
                    b.Left = (b.Bay ? Gd.CleanBayMin : Gd.CleanBedMin) / (Eff(TCleaner) * (Has("quick_clean") ? 1.35 : 1.0));
                    busy[TCleaner]++;
                }
        }

        int FreeBedFor(string ward)
        {
            for (int i = 0; i < S.Beds.Count; i++)
            {
                var b = S.Beds[i];
                if (!b.Bay && b.State == BedState.Clean && b.Patient < 0 && WardOf(b) == ward) return i;
            }
            return -1;
        }

        /// <summary>The ward an admitted patient can go to (its own, or any ward with beds when its own has none built).</summary>
        string TargetWard(Patient p)
        {
            if (BedsIn(p.Ward) > 0) return p.Ward;
            foreach (var w in new[] { "general", "obs", "surgical", "icu" }) if (BedsIn(w) > 0) return w;
            return p.Ward;
        }

        double TripMin(int fromZone, int toZone)
        {
            double t = Gd.PorterBaseMin + Gd.Metres(fromZone, toZone) / Math.Max(1, Gd.WalkMPerMin);
            var z = toZone >= 0 ? Gd.Zones[toZone] : null;
            if (z != null && (z.Ward == "icu" || z.Ward == "surgical")) t += Gd.LiftMin;
            return t / (Eff(TPorter) * (Has("porter_tablets") ? 1.3 : 1.0));
        }

        void AssignPorters()
        {
            if (!Free(TPorter)) return;
            var q = new List<Patient>(Queue(Stage.Board));
            foreach (var p in q)
            {
                if (!Free(TPorter)) return;
                int from = ZoneOfPatient(p);
                if (p.NeedsOr)
                {
                    int ri = S.Rooms.FindIndex(r => r.Kind == "or");
                    if (ri < 0 || S.Rooms[ri].Patient >= 0 || S.Rooms[ri].Down > 0) continue;
                    S.Rooms[ri].Patient = p.Id;
                    StartMove(p, -2 - ri, from, S.Rooms[ri].Zone);
                }
                else
                {
                    int bi = FreeBedFor(TargetWard(p));
                    if (bi < 0) continue;
                    S.Beds[bi].State = BedState.Busy;
                    S.Beds[bi].Patient = p.Id;
                    StartMove(p, bi, from, S.Beds[bi].Zone);
                }
            }
        }

        void StartMove(Patient p, int dest, int fromZone, int toZone)
        {
            FreeBed(p);   // the bay (or the OR) is free the moment the porter wheels the patient out
            p.Stage = Stage.Move;
            p.Dest = dest;
            p.Left = TripMin(fromZone, toZone);
            busy[TPorter]++;
            if (EmitCrowd) Emit("move", p.Id, fromZone, toZone);
        }

        void AssignNurses()
        {
            if (!Free(TNurse)) return;
            double eff = Eff(TNurse);
            // 1 discharge treated ED patients (they hold bays), 2 triage, 3 ward discharges, 4 fast track greens
            foreach (var p in new List<Patient>(Queue(Stage.Ready)))
            {
                if (!Free(TNurse)) return;
                p.Stage = Stage.Discharge; p.Left = Gd.DischargeMin / eff; busy[TNurse]++;
            }
            scratch.Clear();
            foreach (var p in S.Patients) if (p.Stage == Stage.Triage && !p.Guided) scratch.Add(p);   // arrival order
            foreach (var p in new List<Patient>(scratch))
            {
                if (!Free(TNurse)) return;
                p.Stage = Stage.Triaging; p.Left = Gd.TriageMin / eff; busy[TNurse]++;
            }
            scratch.Clear();
            foreach (var p in S.Patients) if ((p.Stage == Stage.WardReady || p.Stage == Stage.Lounge) && !p.Guided) scratch.Add(p);
            foreach (var p in new List<Patient>(scratch))
            {
                if (!Free(TNurse)) return;
                p.Stage = Stage.Discharge; p.Left = Gd.DischargeMin / eff; busy[TNurse]++;
            }
            if (!Has("fast_track")) return;
            foreach (var p in new List<Patient>(Queue(Stage.Bay, x => x.Tag == Sev.Green && !x.Admit)))
            {
                if (!Free(TNurse)) return;
                p.Stage = Stage.Fast; p.Left = Gd.FastTrackMin / eff; busy[TNurse]++;
                MarkSeen(p);
                ChargeSupplies(p);
            }
        }

        void AssignClerks()
        {
            if (!Free(TClerk)) return;
            foreach (var p in S.Patients)
            {
                if (!Free(TClerk)) return;
                if (p.Stage != Stage.Reg || p.Guided) continue;
                p.Stage = Stage.Registering; p.Left = Gd.RegisterMin / Eff(TClerk); busy[TClerk]++;
            }
        }

        double ScanMult(string kind) => kind == "xray" && Has("xray_digital") ? 1.4 : kind == "lab" && Has("lab_automation") ? 1.4 : 1.0;

        void AssignTests()
        {
            for (int ri = 0; ri < S.Rooms.Count; ri++)
            {
                var r = S.Rooms[ri];
                if (r.Kind == "or" || r.Techs <= 0 || r.Down > 0 || r.Patient >= 0) continue;
                var q = Queue(Stage.Test, x => x.Test == r.Kind);
                if (q.Count == 0) continue;
                var p = q[0];
                p.Stage = Stage.Testing;
                p.Room = ri;
                r.Patient = p.Id;
                double scan = Gd.Rooms.TryGetValue(r.Kind, out var rd) ? rd.ScanMin : 15;
                p.Left = scan / (Eff(TTech) * (1 + 0.5 * (r.Techs - 1)) * ScanMult(r.Kind));
                busy[TTech]++;
            }
        }

        int FreeBay()
        {
            for (int i = 0; i < S.Beds.Count; i++)
            {
                var b = S.Beds[i];
                if (b.Bay && b.State == BedState.Clean && b.Patient < 0) return i;
            }
            return -1;
        }

        void AssignBays()
        {
            int bay = FreeBay();
            if (bay < 0) return;
            foreach (var p in new List<Patient>(Queue(Stage.Bay)))
            {
                if (bay < 0) return;
                StartTreat(p, bay);
                bay = FreeBay();
            }
        }

        void StartTreat(Patient p, int bay)
        {
            var b = S.Beds[bay];
            b.State = BedState.Busy;
            b.Patient = p.Id;
            p.Bed = bay;
            p.Stage = Stage.Treat;
            p.Doc = false;
            p.Left = Gd.TreatMin[p.Sev] * (p.Worsened ? 1.25 : 1.0) / (Has("specialist_track") ? 1.2 : 1.0);
            if (p.NoteLost) p.Left += Gd.NoteDelayMin * 0.5;
            ChargeSupplies(p);
            if (EmitCrowd) Emit("bay", p.Id, bay);
        }

        void ChargeSupplies(Patient p)
        {
            double mult = 1;
            foreach (var e in S.Events) if (e.Id == "supply_delay") mult *= e.Mult;
            double c = Math.Round(Gd.Supplies[Math.Max(p.Sev, Math.Max(0, p.Tag))] * mult);   // over-triage uses more supplies
            S.Cash -= c;
            S.Today.Supplies += c;
        }

        void MarkSeen(Patient p)
        {
            if (p.Seen) return;
            p.Seen = true;
            if (p.Guided) return;
            S.Today.WaitSum += p.Wait;
            S.Today.WaitN++;
        }

        void AssignDoctors()
        {
            double eff = Eff(TDoctor);
            // surgery first (the OR is the scarcest room), then bays by priority
            foreach (var p in S.Patients)
            {
                if (!Free(TDoctor)) break;
                if (p.Stage == Stage.Surgery && !p.Doc) { p.Doc = true; p.Left /= eff; busy[TDoctor]++; }
            }
            scratch.Clear();
            foreach (var p in S.Patients) if (p.Stage == Stage.Treat && !p.Doc) scratch.Add(p);
            scratch.Sort((a, b) => { int c = Prio(b).CompareTo(Prio(a)); return c != 0 ? c : a.Id.CompareTo(b.Id); });
            var waiting = new List<Patient>(scratch);
            foreach (var p in waiting)
            {
                if (Free(TDoctor)) { Attach(p, eff); continue; }
                if (p.Tag != Sev.Red) break;
                // a red card takes a doctor from a green / yellow patient (they wait a little longer, nobody is harmed)
                var from = S.Patients.Find(x => x.Stage == Stage.Treat && x.Doc && x.Tag <= Sev.Yellow);
                if (from == null) break;
                from.Doc = false;
                busy[TDoctor]--;
                Attach(p, eff);
            }
        }

        void Attach(Patient p, double eff)
        {
            p.Doc = true;
            if (!p.Seen) p.Left /= eff;   // fatigue stretches the treatment once, when the doctor starts
            MarkSeen(p);
            busy[TDoctor]++;
        }

        // ------------------------------------------------------------------------------------------------ accounting

        void Account(double dt)
        {
            if (night) return;
            var td = S.Today;
            foreach (var b in S.Beds)
            {
                if (b.State == BedState.Closed) continue;
                td.BedMin += dt;
                if (b.State == BedState.Dirty) { td.BlockedMin += dt; continue; }
                if (b.State == BedState.Busy && b.Patient >= 0)
                {
                    var p = P(b.Patient);
                    if (p != null && (p.Stage == Stage.Ready || p.Stage == Stage.Board || p.Stage == Stage.WardReady)) td.BlockedMin += dt;
                }
            }
            double quiet = Has("quiet_hours") ? 0.6 : 1.0;
            for (int t = 0; t < Teams.All.Length; t++)
            {
                string id = Teams.All[t];
                int cap = Avail(t);
                S.CapMin[id] = (S.CapMin.TryGetValue(id, out var c) ? c : 0) + cap * dt;
                S.BusyMin[id] = (S.BusyMin.TryGetValue(id, out var bm) ? bm : 0) + Math.Min(cap, busy[t]) * dt;
                double util = cap > 0 ? Math.Min(1.0, (double)busy[t] / cap) : 0;
                double f = S.Fatigue.TryGetValue(id, out var fv) ? fv : 0;
                f += dt * (util * Gd.FatigueBusy * quiet - (1 - util) * Gd.FatigueIdle);
                S.Fatigue[id] = Math.Max(0, Math.Min(100, f));
            }
        }

        /// <summary>Share of a team's time spent working today (0-1).</summary>
        public double Utilisation(string team)
        {
            double cap = S.CapMin.TryGetValue(team, out var c) ? c : 0;
            return cap <= 0 ? 0 : (S.BusyMin.TryGetValue(team, out var b) ? b : 0) / cap;
        }

        // ------------------------------------------------------------------------------------------------ bottleneck lens

        /// <summary>The stage that blocks the flow now (plan §4 step 5: "tap the red corridor; one sentence").</summary>
        public Bottleneck FindBottleneck()
        {
            var r = new Bottleneck();
            if (S.Tutorial)
            {
                var g = S.Patients.Find(p => p.Guided && p.Stage == Stage.WardReady);
                if (g != null) { r.Key = "discharge"; r.Team = Teams.Nurse; r.Zone = ZoneOfPatient(g); r.Count = S.Patients.FindAll(p => p.Stage == Stage.WardReady).Count; return r; }
            }
            double best = 1.9;
            void Cand(string key, string team, int count, double weight, int zone)
            {
                if (count <= 0) return;
                double s = count * weight;
                if (s > best) { best = s; r.Key = key; r.Team = team; r.Count = count; r.Zone = zone; }
            }
            int ready = 0, readyZone = -1, board = 0, boardZone = -1, porters = 0, porterZone = -1, or = 0, docs = 0, docZone = -1, bays = 0, tri = 0, reg = 0;
            var tests = new Dictionary<string, int>();
            foreach (var p in S.Patients)
            {
                switch (p.Stage)
                {
                    case Stage.Ready: case Stage.WardReady: case Stage.Lounge:
                        ready++; if (readyZone < 0) readyZone = ZoneOfPatient(p); break;
                    case Stage.Board:
                        if (p.NeedsOr) { or++; break; }
                        if (FreeBedFor(TargetWard(p)) < 0) { board++; if (boardZone < 0) boardZone = Gd.ZoneOfWard(TargetWard(p)); }
                        else { porters++; if (porterZone < 0) porterZone = ZoneOfPatient(p); }
                        break;
                    case Stage.Treat: if (!p.Doc) { docs++; if (docZone < 0) docZone = ZoneOfPatient(p); } break;
                    case Stage.Bay: bays++; break;
                    case Stage.Triage: tri++; break;
                    case Stage.Reg: reg++; break;
                    case Stage.Test: if (p.Test != "") tests[p.Test] = (tests.TryGetValue(p.Test, out int n) ? n : 0) + 1; break;
                }
            }
            int dirty = 0, dirtyZone = -1;
            foreach (var b in S.Beds) if (b.State == BedState.Dirty) { dirty++; if (dirtyZone < 0) dirtyZone = b.Zone; }
            bool bedsWanted = bays > 0 || board > 0;
            Cand("discharge", Teams.Nurse, ready, bedsWanted ? 3 : 1.2, readyZone);
            Cand("dirty", Teams.Cleaner, dirty, bedsWanted ? 2.5 : 1, dirtyZone);
            // boarders hold the bays: the bay queue is a ward-bed problem
            Cand("beds", "", board > 0 ? board + (FreeBay() < 0 ? bays : 0) : 0, 3, boardZone);
            Cand("porters", Teams.Porter, porters, 2.5, porterZone);
            Cand("or", "", or, 2, Gd.Zone("or"));
            // a full ED with a patient in a bay and no doctor: the doctors are the limit, not the bays
            Cand("doctors", Teams.Doctor, docs > 0 ? docs + (FreeBay() < 0 ? bays : 0) : 0, 2, docZone);
            Cand("bays", "", docs == 0 && board == 0 && FreeBay() < 0 ? bays : 0, 1.5, Gd.Zone("waiting"));
            Cand("triage", Teams.Nurse, tri, 1, Gd.Zone("triage"));
            Cand("reception", Teams.Clerk, reg, 1, Gd.Zone("reception"));
            foreach (var kv in tests) Cand("tests", Teams.Tech, kv.Value, 1.2, Gd.TestZone(kv.Key));
            return r;
        }

        /// <summary>The player tapped the red corridor / the lens button.</summary>
        public Bottleneck Lens()
        {
            var b = FindBottleneck();
            Emit("lens", b.Zone, b.Count, 0, b.Key);
            return b;
        }

        // ------------------------------------------------------------------------------------------------ player overrides

        /// <summary>The player puts a colour on a patient's card (triage override). Re-tagging a waiting patient is allowed.</summary>
        public string TagPatient(int id, int tag)
        {
            var p = P(id);
            if (p == null || tag < 0 || tag > Sev.Red) return "invalid";
            if (p.Seen || !(Stage.PreDoctor(p.Stage))) return "too_late";
            bool fresh = p.Stage == Stage.Reg || p.Stage == Stage.Registering || p.Stage == Stage.Triage || p.Stage == Stage.Triaging;
            p.Tag = tag;
            if (fresh) AfterTriage(p);
            Emit("tagged", p.Id, tag, tag == p.Sev ? 1 : 0);
            return "";
        }

        /// <summary>The player sends a tagged patient to a free clean bay.</summary>
        public string AssignBay(int id, int bed)
        {
            var p = P(id);
            if (p == null || bed < 0 || bed >= S.Beds.Count || !S.Beds[bed].Bay) return "invalid";
            if (p.Stage != Stage.Bay) return p.Tag < 0 ? "untagged" : "not_ready";
            var b = S.Beds[bed];
            if (b.State != BedState.Clean || b.Patient >= 0) return b.State == BedState.Dirty ? "bay_dirty" : "bay_busy";
            StartTreat(p, bed);
            return "";
        }

        /// <summary>The player signs the discharge now (instead of waiting for a nurse).</summary>
        public string Discharge(int id)
        {
            var p = P(id);
            if (p == null) return "invalid";
            if (p.Stage != Stage.Ready && p.Stage != Stage.WardReady && p.Stage != Stage.Lounge && p.Stage != Stage.Discharge) return "not_ready";
            Leave(p, true);
            return "";
        }

        /// <summary>The player cleans a dirty bed / bay now.</summary>
        public string CleanBed(int bed)
        {
            if (bed < 0 || bed >= S.Beds.Count) return "invalid";
            var b = S.Beds[bed];
            if (b.State != BedState.Dirty && b.State != BedState.Cleaning) return "not_dirty";
            b.State = BedState.Clean;
            b.Left = 0;
            b.Guided = false;
            Emit("cleaned", bed);
            return "";
        }
    }
}
