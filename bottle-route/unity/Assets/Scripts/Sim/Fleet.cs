// Bottle Route sim core — trucks, routes, the route planner, the Dispatcher and technicians.
//   * A truck needs a free driver; it drives the road graph (alleys only for vans and cargo bikes), services each stop
//     (takes what fits by VOLUME and WEIGHT — loose PET is bulky, glass is heavy; a compactor truck squeezes PET), goes
//     home when full or done, and unloads into the depot intake (waits at the gate when the intake is full).
//   * Every km costs fuel (fuel spike ×, fuel saver research, rewarded voucher ½).
//   * The planner ("Suggest" button and the Dispatcher) picks due points by their service policy, nearest-first weighted by
//     urgency, within the truck's capacity; the route optimiser research adds a 2-opt pass. Computed on demand, never per frame.
//   * Ferry (chapter 7): on a ferry edge the truck waits at the pier for the next departure, pays the ticket, sails at the
//     ferry speed and burns no fuel; the estimate adds the expected wait from the planned clock.
using System;
using System.Collections.Generic;

namespace BottleRoute.Sim
{
    public sealed class RouteEstimate
    {
        public bool Ok;
        public string Error = "";
        public double Km, Cost, Minutes, LoadPct;
        public double FerryWait;      // minutes waiting at piers on this route (included in Minutes)
        public int Ferries;           // crossings (tickets, included in Cost)
    }

    public sealed partial class Simulation
    {
        public int BusyTrucks { get { int n = 0; foreach (var t in S.Trucks) if (t.Busy) n++; return n; } }
        public int FreeDrivers => Math.Max(0, S.Drivers - BusyTrucks);
        public int AutoSlots => S.Depot.Dispatcher ? Gd.DispatcherSlots + (Perk("route_pass") ? Gd.PassSlots : 0) + (Perk("dispatcher_plus") ? Gd.PlusSlots : 0) : 0;
        public int AutoCount { get { int n = 0; foreach (var t in S.Trucks) if (t.Auto) n++; return n; } }

        public double LoadVol(TruckState t)
        {
            var v = VehicleOf(t);
            double l = 0;
            for (int m = 0; m < Mats.N; m++) l += t.Load[m] * Gd.Materials[m].VolL / (m == Mats.Pet ? v.PetCompress : 1.0);
            return l;
        }
        public double LoadKg(TruckState t)
        {
            double k = 0;
            for (int m = 0; m < Mats.N; m++) k += t.Load[m] * Gd.Materials[m].Kg;
            return k;
        }
        public double LoadContainers(TruckState t) => t.Load[0] + t.Load[1] + t.Load[2];
        /// <summary>Load bar: the tighter of volume and weight.</summary>
        public double LoadPct(TruckState t)
        {
            var v = VehicleOf(t);
            return Math.Min(1.0, Math.Max(LoadVol(t) / Math.Max(1, v.VolL), LoadKg(t) / Math.Max(1, v.Kg)));
        }
        public bool LoadLimitedByWeight(TruckState t)
        {
            var v = VehicleOf(t);
            return LoadKg(t) / Math.Max(1, v.Kg) > LoadVol(t) / Math.Max(1, v.VolL);
        }

        public bool CanServe(TruckState t, PointState p) => Gd.Graph.Reachable(Gd.DepotNode, p.Node, VehicleOf(t).Alley) && (VehicleOf(t).Alley || !NodeOf(p).Alley);

        /// <summary>Diorama position of a truck (map units = km) and heading (radians).</summary>
        public (double x, double y, double heading) TruckPos(TruckState t)
        {
            if (t.Status == TruckStatus.Drive || (t.Status == TruckStatus.Broken && t.Path.Count > 0)) return Gd.Graph.PointAt(t.Path, t.PathPos);
            var n = Gd.Nodes[Math.Max(0, Math.Min(Gd.Nodes.Count - 1, t.AtNode))];
            return (n.X, n.Y, 0);
        }

        public double FuelMult
        {
            get
            {
                double m = Has("fuel_saver") ? 0.85 : 1.0;
                foreach (var e in S.Events) if (e.Id == "fuel_spike") m *= e.Mult;
                return m;
            }
        }
        public double CostPerKm(TruckState t) => VehicleOf(t).CostKm * FuelMult * (t.Voucher ? 0.5 : 1.0);

        // ------------------------------------------------------------------------------------------------ actions

        /// <summary>Sends an idle truck from the depot along stops (point indices). "" = ok, else a reason code.</summary>
        public string Dispatch(int truck, List<int> stops, bool auto = false)
        {
            if (S.Phase == Phases.Report) return "report";
            if (truck < 0 || truck >= S.Trucks.Count) return "invalid";
            var t = S.Trucks[truck];
            if (t.Busy || t.AtNode != Gd.DepotNode) return "busy";
            if (FreeDrivers <= 0) return "no_driver";
            var clean = new List<int>();
            foreach (int i in stops ?? new List<int>())
            {
                if (i < 0 || i >= S.Points.Count || clean.Contains(i)) continue;
                if (!CanServe(t, S.Points[i])) continue;
                clean.Add(i);
                if (clean.Count >= Math.Max(Gd.MaxStops, 12)) break;
            }
            if (clean.Count == 0) return stops != null && stops.Count > 0 ? "unreachable" : "no_stops";
            t.Stops = clean;
            t.Status = TruckStatus.Drive;
            if (S.FuelVoucher) { t.Voucher = true; S.FuelVoucher = false; }
            if (!auto) t.Auto = t.Auto && S.Depot.Dispatcher;   // a manual route is one run; the truck stays in its mode
            NextLeg(t);
            Emit("dispatch", truck, clean.Count);
            return "";
        }

        /// <summary>Mid-day re-route: adds a point to a truck on the road (or dispatches an idle one with it).</summary>
        public string AddStop(int truck, int point)
        {
            if (truck < 0 || truck >= S.Trucks.Count || point < 0 || point >= S.Points.Count) return "invalid";
            var t = S.Trucks[truck];
            if (!CanServe(t, S.Points[point])) return "unreachable";
            if (!t.Busy) return Dispatch(truck, new List<int> { point });
            if (t.Status == TruckStatus.Unload || t.Status == TruckStatus.Wait) return "busy";
            if (t.Target == point || t.Stops.Contains(point)) return "";
            if (t.Target < 0 && t.Status == TruckStatus.Drive)
            {
                // heading home: turn around for this one stop
                t.Stops.Insert(0, point);
                Reroute(t);
            }
            else t.Stops.Add(point);
            Emit("stop_added", truck, point);
            return "";
        }

        /// <summary>Calls a truck home now (remaining stops dropped).</summary>
        public string Recall(int truck)
        {
            if (truck < 0 || truck >= S.Trucks.Count) return "invalid";
            var t = S.Trucks[truck];
            if (t.Status != TruckStatus.Drive && t.Status != TruckStatus.Service) return "busy";
            t.Stops.Clear();
            if (t.Status == TruckStatus.Drive && t.Target >= 0) { t.Target = -1; Reroute(t); }
            return "";
        }

        public string SetAuto(int truck, bool on)
        {
            if (truck < 0 || truck >= S.Trucks.Count) return "invalid";
            if (on && !S.Depot.Dispatcher) return "no_dispatcher";
            var t = S.Trucks[truck];
            if (on && !t.Auto && AutoCount >= AutoSlots) return "slots";
            t.Auto = on;
            return "";
        }

        /// <summary>Re-plans the current leg from the truck's position: continue to the next stop / home from the next node.</summary>
        void Reroute(TruckState t)
        {
            if (t.Path.Count >= 2 && AtPier(t) && NextFerrySegment(t, out _, out _, out int pier))
            {
                // still on the quay: no crossing, the new leg starts from the pier
                t.AtNode = pier;
                t.FerryAt = 0;
            }
            else if (t.Path.Count >= 2)
            {
                // finish the current road segment first: we treat the truck as standing at the segment's far node
                int seg = SegmentIndex(t);
                double done = SegmentStartKm(t, seg + 1);
                double extra = Math.Max(0, done - t.PathPos);
                if (OnFerry(t)) { extra = 0; t.FerryAt = 0; }   // the boat finishes its crossing (no fuel on the water)
                ChargeKm(t, extra);
                t.AtNode = t.Path[Math.Min(seg + 1, t.Path.Count - 1)];
            }
            NextLeg(t);
        }

        int SegmentIndex(TruckState t)
        {
            double left = t.PathPos;
            for (int i = 1; i < t.Path.Count; i++)
            {
                double seg = Gd.Graph.Km(t.Path[i - 1], t.Path[i], true);
                if (left < seg) return i - 1;
                left -= seg;
            }
            return Math.Max(0, t.Path.Count - 2);
        }

        double SegmentStartKm(TruckState t, int idx)
        {
            double km = 0;
            for (int i = 1; i <= idx && i < t.Path.Count; i++) km += Gd.Graph.Km(t.Path[i - 1], t.Path[i], true);
            return km;
        }

        void NextLeg(TruckState t)
        {
            var v = VehicleOf(t);
            bool full = LoadPct(t) >= 0.97;
            if (full) t.Stops.Clear();
            while (t.Stops.Count > 0)
            {
                int i = t.Stops[0];
                t.Stops.RemoveAt(0);
                if (i < 0 || i >= S.Points.Count) continue;
                if (!CanServe(t, S.Points[i])) continue;
                StartLeg(t, S.Points[i].Node, i);
                return;
            }
            StartLeg(t, Gd.DepotNode, -1);
        }

        void StartLeg(TruckState t, int node, int target)
        {
            var v = VehicleOf(t);
            t.Target = target;
            t.Path = Gd.Graph.Path(t.AtNode, node, v.Alley);
            if (t.Path.Count == 0) t.Path = new List<int> { t.AtNode, node };   // never stuck (data guard)
            t.PathKm = Gd.Graph.PathKm(t.Path);
            t.PathPos = 0;
            t.Status = TruckStatus.Drive;
        }

        void ChargeKm(TruckState t, double km)
        {
            if (km <= 0) return;
            double cost = km * CostPerKm(t);
            S.Cash -= cost;
            S.Today.Fuel += cost;
            S.Today.Km += km;
            t.KmToday += km;
        }

        // ------------------------------------------------------------------------------------------------ step

        void StepTrucks(double dt)
        {
            for (int ti = 0; ti < S.Trucks.Count; ti++)
            {
                var t = S.Trucks[ti];
                var v = VehicleOf(t);
                switch (t.Status)
                {
                    case TruckStatus.Drive:
                    {
                        double limit = t.PathKm, speed = v.SpeedKmh;
                        bool sail = false;
                        if (NextFerrySegment(t, out double fs, out double fe))
                        {
                            if (t.PathPos >= fs - 1e-6)
                            {
                                // at the pier: queue for the next boat (ticket once), then sail at the ferry speed
                                if (t.FerryAt <= 0) { t.FerryAt = NextFerry(S.Minute); PayFerry(t); Emit("ferry_wait", ti, -1, t.FerryAt - S.Minute); }
                                if (S.Minute < t.FerryAt - 1e-9) break;
                                sail = true; limit = fe; speed = Gd.FerrySpeedKmh;
                            }
                            else limit = fs;
                        }
                        double d = Math.Min(speed / 60.0 * dt, Math.Max(0, limit - t.PathPos));
                        t.PathPos += d;
                        if (sail)
                        {
                            if (t.PathPos >= fe - 1e-6) { t.PathPos = Math.Max(t.PathPos, fe); t.FerryAt = 0; }
                            if (t.PathPos >= t.PathKm - 1e-9) Arrive(ti);
                            break;
                        }
                        if (t.PathPos >= limit - 1e-9 && limit < t.PathKm) t.PathPos = limit;   // stop exactly at the pier
                        ChargeKm(t, d);
                        if (JamAllowed && d > 0)
                        {
                            if (Rng.Frand() < d * Gd.BreakdownPerKm * (Has("maint_plan") ? 0.4 : 1.0)) { Breakdown(ti, 60); break; }
                            if (t.Load[Mats.Glass] > 0 && Rng.Frand() < d * Gd.GlassBreakPerKm) GlassBreak(ti);
                        }
                        if (t.PathPos >= t.PathKm - 1e-9) Arrive(ti);
                        break;
                    }
                    case TruckStatus.Service:
                        t.TimeLeft -= dt;
                        if (t.TimeLeft <= 0)
                        {
                            if (t.Target >= 0 && t.Target < S.Points.Count) Pick(ti, t.Target);
                            NextLeg(t);
                        }
                        break;
                    case TruckStatus.Wait:
                        if (S.Depot.IntakeTotal + LoadContainers(t) <= S.Depot.IntakeCap || S.Depot.IntakeTotal <= 0) BeginUnload(t);
                        break;
                    case TruckStatus.Unload:
                        t.TimeLeft -= dt;
                        if (t.TimeLeft <= 0) FinishUnload(ti);
                        break;
                    case TruckStatus.Broken:
                        t.TimeLeft -= dt;
                        if (t.TimeLeft <= 0) { t.Status = string.IsNullOrEmpty(t.ResumeStatus) ? TruckStatus.Drive : t.ResumeStatus; t.ResumeStatus = ""; Emit("truck_fixed", ti); }
                        break;
                }
            }
        }

        void Arrive(int ti)
        {
            var t = S.Trucks[ti];
            t.AtNode = t.Path.Count > 0 ? t.Path[t.Path.Count - 1] : Gd.DepotNode;
            t.Path.Clear();
            t.PathKm = t.PathPos = 0;
            if (t.Target >= 0)
            {
                t.Status = TruckStatus.Service;
                t.TimeLeft = VehicleOf(t).ServiceMin * (Has("fast_service") ? 0.7 : 1.0);
                Emit("arrived", ti, t.Target);
                return;
            }
            if (LoadContainers(t) <= 0) { ToIdle(ti); return; }
            if (S.Depot.IntakeTotal + LoadContainers(t) > S.Depot.IntakeCap && S.Depot.IntakeTotal > 0) { t.Status = TruckStatus.Wait; Emit("gate_queue", ti); }
            else BeginUnload(t);
        }

        void BeginUnload(TruckState t)
        {
            t.Status = TruckStatus.Unload;
            t.TimeLeft = Gd.UnloadBaseMin + LoadContainers(t) * Gd.UnloadPerContainerMin;
        }

        void FinishUnload(int ti)
        {
            var t = S.Trucks[ti];
            double n = LoadContainers(t);
            for (int m = 0; m < Mats.N; m++) { S.Depot.Intake[m] += t.Load[m]; t.Load[m] = 0; }
            t.Trips++;
            Emit("unloaded", ti, -1, n);
            ToIdle(ti);
        }

        void ToIdle(int ti)
        {
            var t = S.Trucks[ti];
            t.Status = TruckStatus.Idle;
            t.AtNode = Gd.DepotNode;
            t.Target = -1;
            t.Stops.Clear();
            t.Path.Clear();
            t.PathKm = t.PathPos = 0;
            t.Voucher = false;
            t.FerryAt = 0;
            Emit("truck_home", ti);
        }

        /// <summary>The crew empties point i into truck ti: as much as fits by volume and weight.</summary>
        void Pick(int ti, int i)
        {
            var t = S.Trucks[ti];
            var p = S.Points[i];
            var v = VehicleOf(t);
            double volRem = Math.Max(0, v.VolL - LoadVol(t));
            double kgRem = Math.Max(0, v.Kg - LoadKg(t));
            double needVol = 0, needKg = 0;
            for (int m = 0; m < Mats.N; m++)
            {
                needVol += p.Fill[m] * Gd.Materials[m].VolL / (m == Mats.Pet ? v.PetCompress : 1.0);
                needKg += p.Fill[m] * Gd.Materials[m].Kg;
            }
            double f = 1.0;
            if (needVol > volRem) f = Math.Min(f, volRem / needVol);
            if (needKg > kgRem) f = Math.Min(f, kgRem / needKg);
            double took = 0;
            for (int m = 0; m < Mats.N; m++)
            {
                double take = Math.Floor(p.Fill[m] * f + 1e-9);
                p.Fill[m] -= take;
                t.Load[m] += take;
                took += take;
            }
            p.Picked += (int)took;
            S.Today.Collected += (int)took;
            S.Containers += (long)took;
            var k = KindOf(p);
            if (k.ShopFee > 0 && took > 0) { double fee = took * k.ShopFee; S.Cash -= fee; S.Today.ShopFees += fee; }
            if (k.PickupBonus > 0 && took > 0) { S.Cash += k.PickupBonus; S.Today.Bonus += k.PickupBonus; }
            p.LastServiceDay = S.Day;
            p.LastServiceMin = S.Minute;
            p.EstVol = Volume(p);
            if (p.SensorOffline) { p.SensorOffline = false; Emit("sensor_fixed", i); }
            // the crew clears a simple jam while they are there (a technician is the fast, remote fix)
            if (p.Jammed) { p.Jammed = false; p.TechCalled = false; Emit("fixed", i, ti); }
            Emit("serviced", ti, i, took);
        }

        // ------------------------------------------------------------------------------------------------ planner

        /// <summary>Is this point due by its own service policy (what the Dispatcher and "Suggest" serve)?</summary>
        public bool Due(PointState p)
        {
            if (p.Jammed) return true;   // a jammed machine needs a visit whatever its rule
            switch (p.Policy)
            {
                case Policies.Fixed: return p.LastServiceDay != S.Day && S.Day - p.LastServiceDay >= Math.Max(1, p.FixedEvery);
                case Policies.Call: return false;
                default: return DisplayPct(p) >= p.Threshold / 100.0 - 1e-9;
            }
        }

        bool Claimed(int i, int exceptTruck)
        {
            for (int k = 0; k < S.Trucks.Count; k++)
            {
                if (k == exceptTruck) continue;
                var t = S.Trucks[k];
                if (t.Busy && (t.Target == i || t.Stops.Contains(i))) return true;
            }
            return false;
        }

        /// <summary>A route for this truck: due points, nearest first weighted by urgency, within the truck's capacity
        /// (estimated from the map bars), ≤ max stops. With anyWorthIt, non-due points join only when they are fairly full
        /// (≥ WorthPct) and cheap to add (insertion detour ≤ WorthDetour × the route); with nothing due the fullest such point
        /// seeds the trip. Nothing qualifying = empty route (leave the truck idle).</summary>
        public List<int> SuggestRoute(int truck, bool anyWorthIt = false)
        {
            var route = new List<int>();
            if (truck < 0 || truck >= S.Trucks.Count) return route;
            var t = S.Trucks[truck];
            var v = VehicleOf(t);
            var cand = new List<int>();
            for (int i = 0; i < S.Points.Count; i++)
            {
                var p = S.Points[i];
                if (!CanServe(t, p) || Claimed(i, truck)) continue;
                if (Due(p) || (anyWorthIt && DisplayPct(p) >= Gd.WorthPct)) cand.Add(i);
            }
            if (anyWorthIt)
            {
                // non-due candidates: keep only the due ones at first, then add the cheap non-due ones in order of fill
                var due = cand.FindAll(i => Due(S.Points[i]));
                var extra = cand.FindAll(i => !Due(S.Points[i]));
                extra.Sort((a, b) => DisplayPct(S.Points[b]).CompareTo(DisplayPct(S.Points[a])));
                var chosen = new List<int>(due);
                if (chosen.Count == 0 && extra.Count > 0) { chosen.Add(extra[0]); extra.RemoveAt(0); }
                foreach (int i in extra)
                {
                    if (chosen.Count >= Gd.MaxStops) break;
                    double before = RouteKm(chosen, v.Alley);
                    var with = new List<int>(chosen);
                    double bestKm = double.MaxValue;
                    for (int pos = 0; pos <= chosen.Count; pos++)
                    {
                        with.Insert(pos, i);
                        bestKm = Math.Min(bestKm, RouteKm(with, v.Alley));
                        with.RemoveAt(pos);
                    }
                    if (bestKm - before <= Gd.WorthDetour * before) chosen.Add(i);
                }
                cand = chosen;
            }
            double vol = 0, kg = 0;
            int at = Gd.DepotNode;
            while (cand.Count > 0 && route.Count < Gd.MaxStops)
            {
                int best = -1;
                double bestScore = double.MaxValue;
                foreach (int i in cand)
                {
                    var p = S.Points[i];
                    double km = Gd.Graph.Km(at, p.Node, v.Alley);
                    double score = km / (0.3 + DisplayPct(p));
                    if (score < bestScore) { bestScore = score; best = i; }
                }
                if (best < 0) break;
                var bp = S.Points[best];
                double estC = DisplayPct(bp) * Cap(bp) / Math.Max(0.1, LitresPerContainer(bp));
                var mix = ExpectedMix(bp);
                double addVol = 0, addKg = 0;
                for (int m = 0; m < Mats.N; m++)
                {
                    addVol += estC * mix[m] * Gd.Materials[m].VolL / (m == Mats.Pet ? v.PetCompress : 1.0);
                    addKg += estC * mix[m] * Gd.Materials[m].Kg;
                }
                if (route.Count > 0 && (vol + addVol > v.VolL * 1.05 || kg + addKg > v.Kg * 1.05)) break;
                vol += addVol; kg += addKg;
                route.Add(best);
                cand.Remove(best);
                at = bp.Node;
            }
            if (Has("route_optimizer")) TwoOpt(route, v.Alley);
            return route;
        }

        double RouteKm(List<int> stops, bool alleys)
        {
            double km = 0;
            int at = Gd.DepotNode;
            foreach (int i in stops) { km += Gd.Graph.Km(at, S.Points[i].Node, alleys); at = S.Points[i].Node; }
            return km + Gd.Graph.Km(at, Gd.DepotNode, alleys);
        }

        void TwoOpt(List<int> r, bool alleys)
        {
            if (r.Count < 3) return;
            bool improved = true;
            int guard = 0;
            while (improved && guard++ < 50)
            {
                improved = false;
                double best = RouteKm(r, alleys);
                for (int a = 0; a < r.Count - 1; a++)
                    for (int b = a + 1; b < r.Count; b++)
                    {
                        r.Reverse(a, b - a + 1);
                        double km = RouteKm(r, alleys);
                        if (km < best - 1e-6) { best = km; improved = true; }
                        else r.Reverse(a, b - a + 1);
                    }
            }
        }

        /// <summary>Predicted km, fuel cost, minutes and load of a route (shown while the player builds it).</summary>
        public RouteEstimate Estimate(int truck, List<int> stops)
        {
            var e = new RouteEstimate();
            if (truck < 0 || truck >= S.Trucks.Count || stops == null || stops.Count == 0) { e.Error = "no_stops"; return e; }
            var t = S.Trucks[truck];
            var v = VehicleOf(t);
            double vol = 0, kg = 0;
            foreach (int i in stops)
            {
                if (i < 0 || i >= S.Points.Count) { e.Error = "invalid"; return e; }
                var p = S.Points[i];
                if (!CanServe(t, p)) { e.Error = "unreachable"; return e; }
                double estC = DisplayPct(p) * Cap(p) / Math.Max(0.1, LitresPerContainer(p));
                var mix = ExpectedMix(p);
                for (int m = 0; m < Mats.N; m++)
                {
                    vol += estC * mix[m] * Gd.Materials[m].VolL / (m == Mats.Pet ? v.PetCompress : 1.0);
                    kg += estC * mix[m] * Gd.Materials[m].Kg;
                }
            }
            // walk the route on a clock: road km at the truck's speed, ferry crossings wait for the next departure
            double start = S.Phase == Phases.Live ? S.Minute : Gd.DayStart, clock = start, road = 0;
            void Leg(int a, int b)
            {
                if (!Gd.Graph.HasFerry || Gd.Graph.FerryKm(a, b, v.Alley) <= 0)
                {
                    double km = Gd.Graph.Km(a, b, v.Alley);
                    road += km; clock += km / Math.Max(1, v.SpeedKmh) * 60;
                    return;
                }
                var path = Gd.Graph.Path(a, b, v.Alley);
                for (int k = 1; k < path.Count; k++)
                {
                    double seg = Gd.Graph.Km(path[k - 1], path[k], v.Alley);
                    if (Gd.Graph.IsFerry(path[k - 1], path[k]))
                    {
                        double w = NextFerry(clock) - clock;
                        e.FerryWait += w; e.Ferries++;
                        clock += w + seg / Math.Max(1, Gd.FerrySpeedKmh) * 60;
                    }
                    else { road += seg; clock += seg / Math.Max(1, v.SpeedKmh) * 60; }
                }
            }
            int at = Gd.DepotNode;
            foreach (int i in stops) { Leg(at, S.Points[i].Node); at = S.Points[i].Node; clock += v.ServiceMin; }
            Leg(at, Gd.DepotNode);
            e.Km = road;
            e.Cost = e.Km * v.CostKm * FuelMult * (S.FuelVoucher ? 0.5 : 1.0) + e.Ferries * Gd.FerryTicket;
            e.Minutes = clock - start + Gd.UnloadBaseMin;
            e.LoadPct = Math.Max(vol / Math.Max(1, v.VolL), kg / Math.Max(1, v.Kg));
            e.Ok = true;
            return e;
        }

        void Dispatcher()
        {
            if (!S.Depot.Dispatcher || S.Minute < S.NextDispatchAt) return;
            S.NextDispatchAt = S.Minute + Math.Max(1, Gd.DispatchEveryMin);
            for (int ti = 0; ti < S.Trucks.Count; ti++)
            {
                var t = S.Trucks[ti];
                if (!t.Auto || t.Busy || FreeDrivers <= 0) continue;
                var r = SuggestRoute(ti);
                if (r.Count > 0) Dispatch(ti, r, true);
            }
            // technicians go to jams and the baler by themselves once the dispatch office exists
            for (int i = 0; i < S.Points.Count && FreeTechs > 0; i++)
                if (S.Points[i].Jammed && !S.Points[i].TechCalled) SendTech("point", i);
            if (S.Depot.BalerDown && FreeTechs > 0 && !S.TechJobs.Exists(j => j.Kind == "baler")) SendTech("baler", 0);
        }

        // ------------------------------------------------------------------------------------------------ technicians

        public int FreeTechs
        {
            get
            {
                int busy = 0;
                foreach (var j in S.TechJobs) if (j.FreeAt > S.Minute) busy++;
                return Math.Max(0, S.Techs - busy);
            }
        }

        /// <summary>kind point (jam / sensor), baler, truck (breakdown). "" = ok.</summary>
        public string SendTech(string kind, int target)
        {
            if (S.Phase == Phases.Report) return "report";
            if (kind == "point" && target >= 0 && target < S.Points.Count && S.Points[target].TechCalled) return "already";
            if (kind == "baler" && S.TechJobs.Exists(j => j.Kind == "baler")) return "already";
            if (FreeTechs <= 0) return "no_tech";
            int node = Gd.DepotNode;
            if (kind == "point")
            {
                if (target < 0 || target >= S.Points.Count) return "invalid";
                var p = S.Points[target];
                if (!p.Jammed && !p.SensorOffline) return "nothing";
                if (p.TechCalled) return "already";
                node = p.Node;
                p.TechCalled = true;
            }
            else if (kind == "baler") { if (!S.Depot.BalerDown) return "nothing"; }
            else if (kind == "truck")
            {
                if (target < 0 || target >= S.Trucks.Count || S.Trucks[target].Status != TruckStatus.Broken) return "nothing";
                var pos = TruckPos(S.Trucks[target]);
                node = NearestNode(pos.x, pos.y);
            }
            else return "invalid";
            double travel = kind == "baler" ? 0 : Gd.Graph.Km(Gd.DepotNode, node) / (Gd.TechSpeedKmh * (Has("tech_van") ? 1.5 : 1.0)) * 60.0;
            var job = new TechJob { Kind = kind, Target = target, DoneAt = S.Minute + travel + Gd.TechFixMin };
            job.FreeAt = job.DoneAt + travel;
            S.TechJobs.Add(job);
            Emit("tech_sent", target, -1, travel + Gd.TechFixMin, kind);
            return "";
        }

        public int NearestNode(double x, double y)
        {
            int best = Gd.DepotNode;
            double bd = double.MaxValue;
            foreach (var n in Gd.Nodes)
            {
                double d = (n.X - x) * (n.X - x) + (n.Y - y) * (n.Y - y);
                if (d < bd) { bd = d; best = n.Index; }
            }
            return best;
        }

        void StepTechs()
        {
            for (int k = S.TechJobs.Count - 1; k >= 0; k--)
            {
                var j = S.TechJobs[k];
                if (j.Kind != "done" && S.Minute >= j.DoneAt) { FinishTech(j); j.Kind = "done"; }
                if (j.Kind == "done" && S.Minute >= j.FreeAt) S.TechJobs.RemoveAt(k);
            }
        }

        void FinishTech(TechJob j)
        {
            if (j.Kind == "point" && j.Target >= 0 && j.Target < S.Points.Count)
            {
                var p = S.Points[j.Target];
                p.Jammed = false; p.SensorOffline = false; p.TechCalled = false; p.BusyUntil = 0;
                Emit("fixed", j.Target);
            }
            else if (j.Kind == "baler") { S.Depot.BalerDown = false; Emit("baler_fixed"); }
            else if (j.Kind == "truck" && j.Target >= 0 && j.Target < S.Trucks.Count && S.Trucks[j.Target].Status == TruckStatus.Broken)
                S.Trucks[j.Target].TimeLeft = 0;
        }
    }
}
