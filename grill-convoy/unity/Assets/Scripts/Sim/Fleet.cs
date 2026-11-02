// Grill Convoy sim core — trucks on the road (the road graph / leg / km-charging code comes from Bottle Route's route
// planner, adapted: a burger truck has ONE destination, its pitch, instead of a multi-stop collection route).
//   * Start of the day: every truck with a pitch drives there (shortest path on the road graph; km × fuel); on arrival the
//     window opens. Travel time matters: a far pitch opens later and a restock run closes the window for the round trip.
//   * Restock run: the truck closes, drives home, reloads its pack list from the commissary, drives back.
//   * Recall: home and closed (the line disperses). Relocate: a different pitch mid-day.
using System;
using System.Collections.Generic;

namespace GrillConvoy.Sim
{
    public sealed partial class Simulation
    {
        /// <summary>Diorama position of a truck (map units = km) and heading (radians).</summary>
        public (double x, double y, double heading) TruckPos(TruckState t)
        {
            if (t.Status == TruckStatus.Drive && t.Path.Count > 0) return Gd.Graph.PointAt(t.Path, t.PathPos);
            var n = NodeAt(t.AtNode);
            return (n.X, n.Y, 0);
        }

        public double FuelMult => Has("fuel_saver") ? 0.85 : 1.0;
        public double CostPerKm(TruckState t) => VehicleOf(t).CostKm * FuelMult;

        /// <summary>Minutes of driving from a to b for this truck (Inf when unreachable).</summary>
        public double DriveMinutes(TruckState t, int a, int b)
        {
            double km = Gd.Graph.Km(a, b);
            return km >= RoadGraph.Inf ? RoadGraph.Inf : km / Math.Max(1, VehicleOf(t).SpeedKmh) * 60.0;
        }

        // ------------------------------------------------------------------------------------------------ actions

        /// <summary>Assigns a pitch for the day (plan) or relocates mid-day. "" = ok, else a reason code.</summary>
        public string AssignPitch(int truck, int node)
        {
            if (truck < 0 || truck >= S.Trucks.Count) return "invalid";
            if (node >= 0 && !PitchOpen(node)) return "locked";
            var t = S.Trucks[truck];
            if (t.Pitch == node) return "";
            t.Pitch = node;
            if (S.Phase == Phases.Live && node >= 0)
            {
                if (t.Status == TruckStatus.Service) { DisperseLine(truck); StartLeg(t, node); }
                else if (t.Status == TruckStatus.Drive && !t.Restocking) Reroute(t, node);
            }
            Emit("pitch", truck, node);
            return "";
        }

        /// <summary>Sends an idle truck (at the commissary) to its pitch now.</summary>
        public string Send(int truck)
        {
            if (S.Phase == Phases.Report) return "report";
            if (truck < 0 || truck >= S.Trucks.Count) return "invalid";
            var t = S.Trucks[truck];
            if (t.Pitch < 0) return "no_pitch";
            if (t.Busy) return "busy";
            if (t.Crew.Count == 0) return "no_crew";
            for (int i = 0; i < Stock.N; i++) t.Pack[i] = t.Load[i];
            StartLeg(t, t.Pitch);
            Emit("dispatch", truck, t.Pitch);
            return "";
        }

        /// <summary>Restock run: close, drive home, reload the pack list, drive back.</summary>
        public string Restock(int truck)
        {
            if (S.Phase != Phases.Live) return "phase";
            if (truck < 0 || truck >= S.Trucks.Count) return "invalid";
            var t = S.Trucks[truck];
            if (t.Status != TruckStatus.Service || t.AtNode == Gd.DepotNode) return "busy";
            DisperseLine(truck);
            t.Restocking = true;
            StartLeg(t, -1);
            Emit("restock_run", truck);
            return "";
        }

        /// <summary>Calls a truck home and closes it for the day.</summary>
        public string Recall(int truck)
        {
            if (truck < 0 || truck >= S.Trucks.Count) return "invalid";
            var t = S.Trucks[truck];
            if (t.Status == TruckStatus.Idle) return "busy";
            if (t.Status == TruckStatus.Restock) { t.Restocking = false; ToIdle(truck); return ""; }
            if (t.Status == TruckStatus.Service) { DisperseLine(truck); t.Restocking = false; StartLeg(t, -1); return ""; }
            t.Restocking = false;
            if (t.Target >= 0) Reroute(t, -1);
            return "";
        }

        public string SetAuto(int truck, bool on)
        {
            if (truck < 0 || truck >= S.Trucks.Count) return "invalid";
            if (on && !Has("dispatcher")) return "research";
            var t = S.Trucks[truck];
            if (on && !t.Auto && AutoCount >= RuleSlots) return "slots";
            t.Auto = on;
            return "";
        }

        public int RuleSlots => Gd.RuleSlots + (Perk("convoy_pass") ? Gd.PassSlots : 0) + (Perk("automation") ? Gd.PlusSlots : 0);
        public int AutoCount { get { int n = 0; foreach (var t in S.Trucks) if (t.Auto) n++; return n; } }

        /// <summary>The line leaves when the truck closes: each waiting customer counts as walked away.</summary>
        void DisperseLine(int truck)
        {
            var t = S.Trucks[truck];
            int n = t.Queue.Count;
            if (n <= 0) return;
            t.Walked += n;
            S.Today.Walked += n;
            t.Queue.Clear();
            t.Work = 0;
            Emit("walked", truck, -1, n, "closed");
        }

        // ------------------------------------------------------------------------------------------------ legs

        /// <summary>Re-plans the current leg from the truck's position: finish the current road segment, then to node.</summary>
        void Reroute(TruckState t, int node)
        {
            if (t.Path.Count >= 2)
            {
                int seg = SegmentIndex(t);
                double done = SegmentStartKm(t, seg + 1);
                ChargeKm(t, Math.Max(0, done - t.PathPos));
                t.AtNode = t.Path[Math.Min(seg + 1, t.Path.Count - 1)];
            }
            StartLeg(t, node);
        }

        int SegmentIndex(TruckState t)
        {
            double left = t.PathPos;
            for (int i = 1; i < t.Path.Count; i++)
            {
                double seg = Gd.Graph.Km(t.Path[i - 1], t.Path[i]);
                if (left < seg) return i - 1;
                left -= seg;
            }
            return Math.Max(0, t.Path.Count - 2);
        }

        double SegmentStartKm(TruckState t, int idx)
        {
            double km = 0;
            for (int i = 1; i <= idx && i < t.Path.Count; i++) km += Gd.Graph.Km(t.Path[i - 1], t.Path[i]);
            return km;
        }

        /// <summary>node -1 = home (the commissary).</summary>
        void StartLeg(TruckState t, int node)
        {
            int dest = node < 0 ? Gd.DepotNode : node;
            t.Target = node;
            t.Path = Gd.Graph.Path(t.AtNode, dest);
            if (t.Path.Count == 0) t.Path = new List<int> { t.AtNode, dest };   // never stuck (data guard)
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
                        double d = Math.Min(v.SpeedKmh / 60.0 * dt, Math.Max(0, t.PathKm - t.PathPos));
                        t.PathPos += d;
                        ChargeKm(t, d);
                        if (t.PathPos >= t.PathKm - 1e-9) Arrive(ti);
                        break;
                    }
                    case TruckStatus.Restock:
                        t.TimeLeft -= dt;
                        if (t.TimeLeft <= 0)
                        {
                            t.Restocking = false;
                            RestockLoad(ti);
                            if (t.Pitch >= 0 && S.Minute < Gd.DayEnd - 30) { StartLeg(t, t.Pitch); Emit("restocked", ti); }
                            else ToIdle(ti);
                        }
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
                t.Work = 0;
                Emit("opened", ti, t.Target);
                return;
            }
            if (t.Restocking)
            {
                t.Status = TruckStatus.Restock;
                t.TimeLeft = Gd.Svc.RestockMin;
                UnloadToCommissary(t, false);
                return;
            }
            ToIdle(ti);
        }

        void ToIdle(int ti)
        {
            var t = S.Trucks[ti];
            t.Status = TruckStatus.Idle;
            t.AtNode = Gd.DepotNode;
            t.Target = -1;
            t.Restocking = false;
            t.Path.Clear();
            t.PathKm = t.PathPos = 0;
            t.Queue.Clear();
            t.Work = 0;
            Emit("home", ti);
        }
    }
}
