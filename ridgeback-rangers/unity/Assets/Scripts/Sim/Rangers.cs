// Ridgeback Rangers sim core — problems as PINS and the ranger teams that answer them (plan §4-5). Teams are scarce, so the
// player prioritises: a pin needs one role (Feeder, Vet, Handler, Fence Tech), the ranger drives the one-lane service road
// (real paths, real minutes; a Feeder fetches crates at the Fodder Barn first), works on site, then drives back to the HQ.
// A pin nobody answered before its deadline FAILS: it costs coins and calm, never progress (no animal or person is hurt:
// a missed vet visit = off exhibit for the day, a missed fence repair may let an animal wander onto a path = the tram
// halts until a Handler escorts it back). The Chief Ranger automation answers pins by itself — late and nearest-first, so a
// careful player still beats it. No expiry and no penalties in the guide.
using System;
using System.Collections.Generic;

namespace RidgebackRangers.Sim
{
    public sealed partial class Simulation
    {
        public PinDef PinDefOf(PinState p) => Gd.Pins.TryGetValue(p.Kind, out var d) ? d : Gd.Pins[PinKinds.Hungry];
        public string RoleFor(string kind) => Gd.Pins.TryGetValue(kind ?? "", out var d) ? d.Role : Roles.Handler;
        public PinState Pin(int id) => S.Pins.Find(p => p.Id == id);
        public int RangerIndex(int id) => S.Rangers.FindIndex(r => r.Id == id);
        public bool ChiefAvailable => Has("chief") || Perk("ranger_pass");
        public int CountRole(string role) { int n = 0; foreach (var r in S.Rangers) if (r.Role == role && !r.Temp) n++; return n; }
        public double RangerKmh => Gd.RangerKmh * (Has("lane") ? Gd.LaneMult : 1.0);

        /// <summary>Opens a pin (deduplicated: one per paddock and kind, one per sick animal, one flood per zone). Returns its id or -1.</summary>
        public int OpenPin(string kind, int paddock, int animal, string severity = "")
        {
            if (!PinKinds.Valid(kind)) return -1;
            string zone = paddock >= 0 && paddock < S.Paddocks.Count ? ZoneOf(S.Paddocks[paddock]) : "";
            foreach (var p in S.Pins)
            {
                if (p.Kind != kind) continue;
                if (kind == PinKinds.Sick ? p.Animal == animal : kind == PinKinds.Flood ? p.Zone == zone : p.Paddock == paddock) return -1;
            }
            int node = kind == PinKinds.Escape || kind == PinKinds.Flood ? Gd.ViewOf(zone) : paddock >= 0 ? S.Paddocks[paddock].Node : -1;
            if (node < 0) return -1;
            var def = Gd.Pins[kind];
            double deadline = def.Deadline;
            if (kind == PinKinds.Sick)
            {
                int k = Array.IndexOf(Health.Sick, severity);
                if (k >= 0 && k < Gd.SickDeadline.Length) deadline = Gd.SickDeadline[k];
            }
            var pin = new PinState { Id = S.NextId++, Kind = kind, Node = node, Paddock = paddock, Animal = animal, Zone = zone, Severity = severity, Created = S.Minute, Deadline = S.Minute + deadline };
            S.Pins.Add(pin);
            S.Today.PinsOpened++;
            Emit("pin", pin.Id, paddock, 0, kind);
            return pin.Id;
        }

        /// <summary>A pin for the guide / tests / a Ranger Call: picks a sensible paddock or animal by itself.</summary>
        public int ForcePin(string kind, int paddock = -1)
        {
            if (paddock < 0 || paddock >= S.Paddocks.Count)
            {
                paddock = -1;
                for (int p = 0; p < S.Paddocks.Count; p++) if (AnimalsIn(p).Count > 0) { paddock = p; if (Rng.Frand() < 0.5) break; }
                if (paddock < 0) return -1;
            }
            var ids = AnimalsIn(paddock);
            switch (kind)
            {
                case PinKinds.Sick:
                {
                    if (ids.Count == 0) return -1;
                    int i = ids[Rng.RangeI(0, ids.Count - 1)];
                    if (S.Animals[i].Health != Health.Ok) return -1;
                    S.Animals[i].Health = Health.Green;
                    return OpenPin(kind, paddock, i, Health.Green);
                }
                case PinKinds.Hungry:
                    foreach (int i in ids) S.Animals[i].Hunger = Math.Max(S.Animals[i].Hunger, Gd.HungryPinAt);
                    if (S.Stations.TryGetValue(ZoneOf(S.Paddocks[paddock]), out var st)) st.Crates = 0;
                    return OpenPin(kind, paddock, -1);
                case PinKinds.Fence:
                    S.Paddocks[paddock].Integrity = Math.Min(S.Paddocks[paddock].Integrity, Gd.FencePinBelow - 0.05);
                    return OpenPin(kind, paddock, -1);
                case PinKinds.Escape:
                    return ids.Count == 0 ? -1 : OpenPin(kind, paddock, ids[0]);
                default:
                    return OpenPin(kind, paddock, -1);
            }
        }

        void StepPins(double dt)
        {
            if (S.Tutorial) return;
            for (int i = S.Pins.Count - 1; i >= 0; i--)
            {
                var p = S.Pins[i];
                if (p.Ranger >= 0) continue;   // someone is on the way: the pin waits for them
                if (S.Minute >= p.Deadline) Fail(p);
            }
            if (!(S.ChiefOn && ChiefAvailable)) return;
            // the Chief Ranger: oldest pin first, nearest free ranger, only after a delay (a careful player is faster)
            PinState best = null;
            foreach (var p in S.Pins)
                if (p.Ranger < 0 && S.Minute - p.Created >= Gd.ChiefDelayMin && (best == null || p.Created < best.Created) && FreeRanger(RoleFor(p.Kind), p.Node) >= 0) best = p;
            if (best != null) Send(best.Id, FreeRanger(RoleFor(best.Kind), best.Node));
        }

        void Fail(PinState p)
        {
            S.Pins.Remove(p);
            var def = PinDefOf(p);
            S.Today.PinsFailed++;
            S.Today.Failed.Add(p.Kind);
            S.Today.Fines += def.Fine;
            S.Cash -= def.Fine;
            switch (p.Kind)
            {
                case PinKinds.Hungry:
                case PinKinds.Squabble:
                    foreach (int i in AnimalsIn(p.Paddock)) S.Animals[i].Mood = Math.Max(0, S.Animals[i].Mood - (p.Kind == PinKinds.Hungry ? 0.2 : 0.25));
                    break;
                case PinKinds.Sick:
                    if (p.Animal >= 0 && p.Animal < S.Animals.Count) S.Animals[p.Animal].Health = Health.Off;   // off exhibit for the day, back tomorrow
                    break;
                case PinKinds.Fence:
                    if (p.Paddock >= 0 && p.Paddock < S.Paddocks.Count)
                    {
                        S.Paddocks[p.Paddock].Integrity = Math.Max(0, S.Paddocks[p.Paddock].Integrity - 0.2);
                        var ids = AnimalsIn(p.Paddock);
                        if (ids.Count > 0 && Rng.Frand() < Gd.EscapeChance) OpenPin(PinKinds.Escape, p.Paddock, ids[Rng.RangeI(0, ids.Count - 1)]);
                    }
                    break;
                case PinKinds.Escape:
                {
                    // evacuation: the queue goes home unhappy (nobody is hurt; the island crew walks the animal back)
                    int n = Queued;
                    S.Queue.Clear();
                    S.Today.SatN += n;
                    if (p.Paddock >= 0 && p.Paddock < S.Paddocks.Count) S.Paddocks[p.Paddock].Integrity = Math.Max(S.Paddocks[p.Paddock].Integrity, 0.7);
                    Emit("evacuation", n);
                    break;
                }
            }
            Emit("pin_failed", p.Id, p.Paddock, def.Fine, p.Kind);
        }

        void Resolve(PinState p)
        {
            S.Pins.Remove(p);
            S.Today.PinsResolved++;
            switch (p.Kind)
            {
                case PinKinds.Hungry:
                {
                    string zone = p.Paddock >= 0 ? ZoneOf(S.Paddocks[p.Paddock]) : "";
                    if (S.Stations.TryGetValue(zone, out var st)) st.Crates = Math.Min(StationCap(zone), st.Crates + Gd.FeederCarry);
                    foreach (int i in AnimalsIn(p.Paddock))
                    {
                        var a = S.Animals[i];
                        if (a.Hunger >= Gd.EatAt) { a.Hunger = Math.Max(0.1, a.Hunger - Gd.EatGain); a.ShowUntil = S.Minute + Gd.EatShowMin; }
                    }
                    Emit("fed", p.Id, p.Paddock);
                    break;
                }
                case PinKinds.Squabble:
                    foreach (int i in AnimalsIn(p.Paddock)) S.Animals[i].Mood = Math.Min(1, S.Animals[i].Mood + 0.1);
                    break;
                case PinKinds.Sick:
                    if (p.Animal >= 0 && p.Animal < S.Animals.Count && S.Animals[p.Animal].Health != Health.Off) S.Animals[p.Animal].Health = Health.Ok;
                    break;
                case PinKinds.Fence:
                    if (p.Paddock >= 0 && p.Paddock < S.Paddocks.Count) S.Paddocks[p.Paddock].Integrity = 1;
                    break;
            }
            Emit("pin_done", p.Id, p.Paddock, 0, p.Kind);
        }

        // ------------------------------------------------------------------------------------------------ rangers

        /// <summary>The node a ranger is at (idle / working) or about to reach (driving).</summary>
        public int CurrentNode(RangerState r)
        {
            if ((r.Status == RangerStatus.Travel || r.Status == RangerStatus.Back) && r.Path.Count > 0)
            {
                double km = 0;
                for (int i = 1; i < r.Path.Count; i++)
                {
                    km += Gd.Graph.Km(r.Path[i - 1], r.Path[i]);
                    if (km >= r.PathPos - 1e-9) return r.Path[i];
                }
                return r.Path[r.Path.Count - 1];
            }
            return r.AtNode;
        }

        /// <summary>A free ranger of the role (idle, or driving home) nearest to node; -1 = none.</summary>
        public int FreeRanger(string role, int node)
        {
            int best = -1; double bd = double.MaxValue;
            for (int i = 0; i < S.Rangers.Count; i++)
            {
                var r = S.Rangers[i];
                if (r.Role != role || r.Busy || r.Temp) continue;
                double d = node >= 0 ? Gd.Graph.Km(CurrentNode(r), node) : 0;
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        public int FreeCount(string role) { int n = 0; foreach (var r in S.Rangers) if (r.Role == role && !r.Busy && !r.Temp) n++; return n; }

        void Route(RangerState r, int target)
        {
            int from = CurrentNode(r);
            r.Path = Gd.Graph.Path(from, target);
            if (r.Path.Count == 0) r.Path = new List<int> { from, target };
            r.PathKm = Gd.Graph.PathKm(r.Path);
            r.PathPos = 0;
            r.AtNode = from;
        }

        /// <summary>Minutes from the ranger's spot to the pin (what the pin card shows before sending).</summary>
        public double EtaMin(int rangerIndex, int pinId)
        {
            var p = Pin(pinId);
            if (p == null || rangerIndex < 0 || rangerIndex >= S.Rangers.Count) return 0;
            var r = S.Rangers[rangerIndex];
            int from = CurrentNode(r);
            double km = p.Kind == PinKinds.Hungry && r.Role == Roles.Feeder ? Gd.Graph.Km(from, Gd.BarnNode) + Gd.Graph.Km(Gd.BarnNode, p.Node) : Gd.Graph.Km(from, p.Node);
            return km / Math.Max(0.1, RangerKmh) * 60.0;
        }

        /// <summary>Sends ranger (index) to the pin. "" = ok, else invalid | role | busy | taken.</summary>
        public string Send(int pinId, int rangerIndex)
        {
            var p = Pin(pinId);
            if (p == null || rangerIndex < 0 || rangerIndex >= S.Rangers.Count) return "invalid";
            if (S.Phase != Phases.Live) return "phase";
            var r = S.Rangers[rangerIndex];
            if (r.Role != RoleFor(p.Kind)) return "role";
            if (r.Busy || r.Temp) return "busy";
            if (p.Ranger >= 0) return "taken";
            p.Ranger = r.Id;
            r.Pin = p.Id;
            r.Dig = -1;
            if (p.Kind == PinKinds.Hungry && r.Role == Roles.Feeder) { r.Leg = 1; Route(r, Gd.BarnNode); }
            else { r.Leg = 2; Route(r, p.Node); }
            r.Status = RangerStatus.Travel;
            Emit("sent", p.Id, rangerIndex, 0, p.Kind);
            return "";
        }

        double WorkMin(RangerState r, PinState p)
        {
            double w = Gd.RoleDefs.TryGetValue(r.Role, out var d) ? d.WorkMin : 10;
            if (r.Role == Roles.Vet && Has("vet_beds")) w = Gd.VetBedsWorkMin;
            if (p != null && p.Kind == PinKinds.Sick && p.Severity == Health.Green) w *= 0.6;
            return w;
        }

        void GoHome(RangerState r)
        {
            r.Pin = -1;
            r.Leg = 0;
            r.TimeLeft = 0;
            if (CurrentNode(r) == Gd.HqNode) { r.Status = RangerStatus.Idle; r.AtNode = Gd.HqNode; r.Path.Clear(); r.PathKm = r.PathPos = 0; return; }
            Route(r, Gd.HqNode);
            r.Status = RangerStatus.Back;
        }

        void StepRangers(double dt)
        {
            double km = RangerKmh * dt / 60.0;
            for (int i = S.Rangers.Count - 1; i >= 0; i--)
            {
                var r = S.Rangers[i];
                switch (r.Status)
                {
                    case RangerStatus.Travel:
                    case RangerStatus.Back:
                        r.PathPos += km;
                        if (r.PathPos + 1e-9 < r.PathKm) break;
                        r.PathPos = r.PathKm;
                        r.AtNode = r.Path.Count > 0 ? r.Path[r.Path.Count - 1] : r.AtNode;
                        if (r.Status == RangerStatus.Back)
                        {
                            if (r.Temp) { S.Rangers.RemoveAt(i); break; }
                            r.Status = RangerStatus.Idle;
                            r.Path.Clear(); r.PathKm = r.PathPos = 0;
                            break;
                        }
                        if (r.Dig >= 0) { r.Status = RangerStatus.Dig; r.TimeLeft = Gd.DigWorkMin; break; }
                        var pin = Pin(r.Pin);
                        if (pin == null) { GoHome(r); break; }
                        if (r.Leg == 1) { r.Leg = 2; Route(r, pin.Node); break; }   // crates loaded at the barn
                        r.Status = RangerStatus.Work;
                        r.TimeLeft = WorkMin(r, pin);
                        break;
                    case RangerStatus.Work:
                        r.TimeLeft -= dt;
                        if (r.TimeLeft > 0) break;
                        var p = Pin(r.Pin);
                        if (p != null) Resolve(p);
                        if (r.Temp) { S.Rangers.RemoveAt(i); break; }   // the borrowed ranger heads back to the next ridge
                        GoHome(r);
                        break;
                    case RangerStatus.Dig:
                        r.TimeLeft -= dt;
                        if (r.TimeLeft > 0) break;
                        FinishDig(r);
                        GoHome(r);
                        break;
                }
            }
        }

        /// <summary>Where a ranger is (map km) for the diorama.</summary>
        public (double x, double y, double heading) RangerPoint(RangerState r)
        {
            if ((r.Status == RangerStatus.Travel || r.Status == RangerStatus.Back) && r.Path.Count > 0) return Gd.Graph.PointAt(r.Path, r.PathPos);
            var n = Gd.Nodes[Math.Max(0, Math.Min(Gd.Nodes.Count - 1, r.AtNode))];
            return (n.X, n.Y, 0);
        }

        /// <summary>Day end: everyone home; a pin with a ranger on it counts as done, an old unanswered one as ignored (fails),
        /// a young one carries over to the morning (same time left); floods drain with the rain.</summary>
        void CloseDayPins()
        {
            foreach (var p in new List<PinState>(S.Pins))
            {
                if (p.Ranger >= 0) Resolve(p);
                else if (S.Tutorial || p.Kind == PinKinds.Flood) S.Pins.Remove(p);
                else if (S.Minute - p.Created >= (p.Deadline - p.Created) * 0.5) Fail(p);
                else
                {
                    double left = p.Deadline - S.Minute, age = S.Minute - p.Created;
                    p.Deadline = Gd.DayStart + left;
                    p.Created = Gd.DayStart - age;
                }
            }
            for (int i = S.Rangers.Count - 1; i >= 0; i--)
            {
                var r = S.Rangers[i];
                if (r.Temp) { S.Rangers.RemoveAt(i); continue; }
                if (r.Status == RangerStatus.Dig) FinishDig(r);
                else if (r.Dig >= 0) { S.Cp += Gd.DigCp; r.Dig = -1; }   // never reached the site: the dig is refunded
                r.Status = RangerStatus.Idle; r.AtNode = Gd.HqNode; r.Pin = -1; r.Leg = 0; r.TimeLeft = 0; r.Path.Clear(); r.PathKm = r.PathPos = 0;
            }
        }
    }
}
