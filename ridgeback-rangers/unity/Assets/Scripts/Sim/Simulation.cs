// Ridgeback Rangers sim core — engine independent (no UnityEngine). C# 9 / .NET Standard 2.1.
// One career: a coastal region's deposit-return network. A work day runs 09:00-19:00 game time (~4 real minutes at 1x).
//   plan  (09:00, paused)  — routes, policies, purchases;
//   live                   — citizens bring bottles to return points, trucks drive routes, the depot counts and bales;
//   report (19:00)         — trucks come home, the depot wraps up, costs are paid, stars + Green Rating; NextDay().
// Partial class: this file = construction, time, demand (citizens); Fleet.cs = trucks, routes, technicians;
// Depot.cs = count line, baler, market; Economy.cs = day end, purchases, research, offline, rewards; Crises.cs = events.
using System;
using System.Collections.Generic;

namespace RidgebackRangers.Sim
{
    public sealed class SimEvent
    {
        public string Type;     // deposit, turned, jam, serviced, unloaded, bale, sold, crisis, research_done, district, day_done, day_start, ...
        public int A = -1, B = -1;
        public double V;
        public string Text = "";
        public override string ToString() => Type + "(" + A + "," + B + "," + V + "," + Text + ")";
    }

    public sealed partial class Simulation
    {
        public readonly GameData Gd;
        public State S;
        public readonly Rng Rng;
        readonly List<SimEvent> events = new List<SimEvent>();
        readonly Dictionary<string, bool> perks = new Dictionary<string, bool>();
        double[] litresPerContainer;  // per point node: expected litres per deposited container (estimate model)
        /// <summary>Emit deposit / turned events for the diorama's walkers (off in balance bots).</summary>
        public bool EmitCrowd = true;

        public Simulation(GameData gd, long seed)
        {
            Gd = gd ?? throw new ArgumentNullException(nameof(gd));
            S = new State { Seed = seed <= 0 ? 1 : seed };
            Rng = new Rng(S.Seed);
            NewCareer();
            S.RngState = Rng.State;
        }

        /// <summary>From a decoded save (SaveCodec).</summary>
        public Simulation(GameData gd, State s)
        {
            Gd = gd ?? throw new ArgumentNullException(nameof(gd));
            S = s;
            Rng = new Rng(1) { State = s.RngState <= 0 ? 1 : s.RngState };
        }

        public JObj Snapshot()
        {
            S.RngState = Rng.State;
            return S.ToJson();
        }

        void NewCareer()
        {
            S.Day = 1;
            S.Minute = Gd.DayStart;
            S.Phase = Phases.Plan;
            S.Cash = Gd.StartCash;
            S.Rating = Gd.StartRating;
            S.Drivers = Gd.StartDrivers;
            S.Techs = Gd.StartTechs;
            S.Districts.Add(Gd.Districts.Count > 0 ? Gd.Districts[0].Id : "harbour");
            S.Depot = new DepotState { Lines = Gd.LinesStart, YardCap = Gd.YardCap, IntakeCap = Gd.IntakeCap };
            S.Depot.BalerIn[Mats.Pet] = Gd.StartBalerPetKg;
            foreach (var id in Gd.StartMachines)
            {
                int node = Gd.Node(id);
                var p = new PointState { Node = node, Kind = "machine_s", LastServiceDay = 0 };
                S.Points.Add(p);
                if (Gd.StartFill.TryGetValue(id, out double pct)) FillTo(p, pct);
            }
            int tid = 0;
            foreach (var type in Gd.StartTrucks) S.Trucks.Add(new TruckState { Id = tid++, Type = type, AtNode = Gd.DepotNode });
            S.Today = new DayStats { Day = 1 };
            S.NextDispatchAt = Gd.DayStart;
        }

        /// <summary>Fills a point to pct of its capacity with the district's usual mix (start state, tests).</summary>
        public void FillTo(PointState p, double pct)
        {
            var mix = ExpectedMix(p);
            double target = Cap(p) * Math.Max(0, pct);
            double perContainer = 0;
            for (int m = 0; m < Mats.N; m++) perContainer += mix[m] * VolInPoint(p, m);
            if (perContainer <= 0) return;
            double n = Math.Floor(target / perContainer);
            for (int m = 0; m < Mats.N; m++) p.Fill[m] = Math.Floor(n * mix[m]);
            p.EstVol = Volume(p);
        }

        // ------------------------------------------------------------------------------------------------ queries

        public List<SimEvent> DrainEvents()
        {
            var o = new List<SimEvent>(events);
            events.Clear();
            return o;
        }

        void Emit(string type, int a = -1, int b = -1, double v = 0, string text = "") => events.Add(new SimEvent { Type = type, A = a, B = b, V = v, Text = text ?? "" });

        public string Phase => S.Phase;
        public NodeDef NodeOf(PointState p) => Gd.Nodes[p.Node];
        public PointKindDef KindOf(PointState p) => Gd.PointKinds.TryGetValue(p.Kind, out var k) ? k : Gd.PointKinds["machine_s"];
        public VehicleDef VehicleOf(TruckState t) => Gd.Vehicles.TryGetValue(t.Type, out var v) ? v : Gd.Vehicles["van"];
        public bool Has(string research) => S.Research.Contains(research);
        public bool Unlocked(string district) => S.Districts.Contains(district);
        public bool Perk(string id) => perks.TryGetValue(id, out bool b) && b;
        public int Chapter
        {
            get
            {
                int c = 1;
                foreach (var d in Gd.Districts) if (Unlocked(d.Id)) c = Math.Max(c, d.Chapter);
                return c;
            }
        }
        public int MaxSpeed => Perk("speed") ? Gd.PaidMaxSpeed : Gd.FreeMaxSpeed;
        public double FeeMult => (Gd.FeeRatingMin + Gd.FeeRatingSpan * Math.Max(0, Math.Min(100, S.Rating)) / 100.0) * (Perk("route_pass") ? 1.10 : 1.0);
        public int HourIndex => Math.Max(0, Math.Min(Gd.HourCurve.Length - 1, (int)((S.Minute - Gd.DayStart) / 60.0)));
        public bool Weekend => (S.Day - 1) % 7 >= 5;
        public int PointAt(int node) => S.Points.FindIndex(p => p.Node == node);

        /// <summary>Litres one container of material m takes inside this point (compactor squeezes PET).</summary>
        public double VolInPoint(PointState p, int m) => Gd.Materials[m].VolL / (m == Mats.Pet && p.Compactor ? Gd.CompactorPet : 1.0);
        public double Cap(PointState p) => KindOf(p).CapL;
        public double Volume(PointState p)
        {
            double v = 0;
            for (int m = 0; m < Mats.N; m++) v += p.Fill[m] * VolInPoint(p, m);
            return v;
        }
        public double FillPct(PointState p) => Math.Min(1.0, Volume(p) / Math.Max(1, Cap(p)));
        public double Containers(PointState p) => p.Fill[0] + p.Fill[1] + p.Fill[2];
        public bool IsFull(PointState p) => FillPct(p) >= 0.97;

        /// <summary>What the map shows: live with a working sensor, a frozen number when the sensor / app is wrong, an
        /// estimate from history (no events, no weekend) without a sensor — the "fuzzy" bar of the design.</summary>
        public double DisplayPct(PointState p)
        {
            if (p.SensorOffline) return p.FrozenPct;
            if (p.Sensor) return FillPct(p);
            return Math.Min(1.0, Math.Max(0, p.EstVol) / Math.Max(1, Cap(p)));
        }
        public bool DisplayIsLive(PointState p) => p.Sensor && !p.SensorOffline;

        double[] ExpectedMix(PointState p)
        {
            var k = KindOf(p);
            if (!k.Citizens) return k.Mix;
            var mix = new double[Mats.N];
            var w = DistrictWeights(NodeOf(p).District, false);
            double tw = 0;
            for (int a = 0; a < Gd.Archetypes.Count; a++)
            {
                double ww = w[a] * Gd.Archetypes[a].Mean;
                tw += ww;
                for (int m = 0; m < Mats.N; m++) mix[m] += ww * Gd.Archetypes[a].Mix[m];
            }
            if (tw > 0) for (int m = 0; m < Mats.N; m++) mix[m] /= tw;
            return mix;
        }

        double LitresPerContainer(PointState p)
        {
            if (litresPerContainer == null) litresPerContainer = new double[Gd.Nodes.Count];
            int key = p.Node;
            // recomputed when 0 (compactor upgrades reset it)
            if (litresPerContainer[key] <= 0)
            {
                var mix = ExpectedMix(p);
                double l = 0;
                for (int m = 0; m < Mats.N; m++) l += mix[m] * VolInPoint(p, m);
                litresPerContainer[key] = Math.Max(0.1, l);
            }
            return litresPerContainer[key];
        }
        void ResetLitreCache() => litresPerContainer = null;

        double[] DistrictWeights(string district, bool timeAdjusted)
        {
            var d = Gd.District(district);
            var w = new double[Gd.Archetypes.Count];
            for (int a = 0; a < w.Length; a++)
            {
                double x = d != null && a < d.Archetypes.Length ? d.Archetypes[a] : 1.0 / w.Length;
                if (timeAdjusted)
                {
                    if (Weekend) x *= Gd.Archetypes[a].Weekend;
                    if (S.Minute >= 1020) x *= Gd.Archetypes[a].Evening;   // from 17:00
                }
                w[a] = x;
            }
            return w;
        }

        /// <summary>Base containers per hour at a point now (site rate × hour curve), without events or weekend.</summary>
        public double BaseRate(PointState p) => NodeOf(p).Rate * Gd.HourCurve[HourIndex];

        /// <summary>Event multiplier for a point now (heat wave, match day window).</summary>
        public double DemandMult(PointState p)
        {
            double m = Weekend ? Gd.WeekendMult : 1.0;
            foreach (var e in S.Events)
            {
                if (e.Id == "heat_wave") m *= e.Mult;
                else if (e.Id == "match_day" && NodeOf(p).EventSite && S.Minute >= e.From && S.Minute < e.To) m *= e.Mult;
            }
            return m;
        }

        // ------------------------------------------------------------------------------------------------ time

        public void SetSpeed(int speed) => S.Speed = Math.Max(1, Math.Min(MaxSpeed, speed));
        public void SetPaused(bool paused) => S.Paused = paused;

        /// <summary>Store perks (never saved): premium, route_pass, dispatcher_plus, offline_plus, speed.</summary>
        public void SetPerks(Dictionary<string, bool> p)
        {
            perks.Clear();
            if (p != null) foreach (var kv in p) perks[kv.Key] = kv.Value;
            if (S.Speed > MaxSpeed) S.Speed = MaxSpeed;
            int slots = AutoSlots;
            int n = 0;
            foreach (var t in S.Trucks) if (t.Auto && ++n > slots) t.Auto = false;
        }

        /// <summary>Every frame with real seconds. Research runs on real time in every phase; the day only while live.</summary>
        public void Advance(double realDt)
        {
            if (realDt <= 0 || double.IsNaN(realDt)) return;
            realDt = Math.Min(realDt, 1.0);   // a hitch never jumps the sim
            TickResearch(realDt);
            if (S.Phase != Phases.Live || S.Paused) return;
            double gm = realDt * Gd.GameMinPerRealS * Math.Max(1, S.Speed);
            while (gm > 1e-9 && S.Phase == Phases.Live)
            {
                double step = Math.Min(1.0, gm);
                Step(step);
                gm -= step;
            }
        }

        /// <summary>Advances game minutes directly (tests, guide fast-forward). Stops at the day end.</summary>
        public void AdvanceMinutes(double minutes)
        {
            while (minutes > 1e-9 && S.Phase == Phases.Live)
            {
                double step = Math.Min(1.0, minutes);
                Step(step);
                minutes -= step;
            }
        }

        void Step(double dt)
        {
            RunPlanned();
            Demand(dt);
            StepTrucks(dt);
            StepTechs();
            StepDepot(dt);
            foreach (var p in S.Points)
                if (IsFull(p) || p.Jammed) { p.FullMin += dt; S.Today.FullMin += dt; }
            Dispatcher();
            S.Minute += dt;
            if (S.Minute >= Gd.DayEnd - 1e-9) EndDay();
        }

        // ------------------------------------------------------------------------------------------------ demand

        void Demand(double dt)
        {
            for (int i = 0; i < S.Points.Count; i++)
            {
                var p = S.Points[i];
                var kind = KindOf(p);
                double baseRate = BaseRate(p);
                double lpc = LitresPerContainer(p);
                p.EstVol += baseRate * dt / 60.0 * lpc;
                double lambda = baseRate * DemandMult(p) * dt / 60.0;   // containers this step
                if (!kind.Citizens)
                {
                    // HoReCa: staff drop bags of ~10 into the back room
                    double bags = lambda / 10.0;
                    int k = Sample(bags);
                    for (int b = 0; b < k; b++) Deposit(i, Rng.RangeI(8, 14), kind.Mix, -1);
                    continue;
                }
                var w = DistrictWeights(NodeOf(p).District, true);
                double tw = 0, mean = 0;
                for (int a = 0; a < w.Length; a++) { tw += w[a]; mean += w[a] * Gd.Archetypes[a].Mean; }
                if (tw <= 0) continue;
                mean /= tw;
                int citizens = Sample(lambda / Math.Max(1, mean));
                for (int c = 0; c < citizens; c++)
                {
                    int a = Rng.PickWeighted(w);
                    if (a < 0) continue;
                    var ar = Gd.Archetypes[a];
                    Deposit(i, Rng.RangeI(ar.Min, ar.Max), ar.Mix, a);
                }
            }
        }

        int Sample(double expected)
        {
            if (expected <= 0) return 0;
            int k = (int)Math.Floor(expected);
            if (Rng.Frand() < expected - k) k++;
            return k;
        }

        /// <summary>One citizen (or a HoReCa bag) at point i with n containers. Returns accepted containers.</summary>
        public int Deposit(int i, int n, double[] mix, int archetype)
        {
            var p = S.Points[i];
            p.Arrivals++;
            S.Today.Arrivals++;
            if (p.Jammed || p.BusyUntil > S.Minute)
            {
                Turn(i, archetype);
                return 0;
            }
            double free = Cap(p) - Volume(p);
            int accepted = 0, rejected = 0;
            for (int c = 0; c < n; c++)
            {
                int m = Rng.PickWeighted(mix);
                if (m < 0) m = 0;
                double v = VolInPoint(p, m);
                if (v <= free) { p.Fill[m] += 1; free -= v; accepted++; }
                else rejected++;
            }
            p.Deposited += accepted;
            S.Today.Deposited += accepted;
            if (rejected > 0) Turn(i, archetype);
            else if (EmitCrowd) Emit("deposit", i, archetype, accepted);
            if (accepted > 0 && p.IsMachine && !p.Jammed && JamAllowed)
            {
                double q = KindOf(p).JamPer1000 / 1000.0 * (p.Compactor ? Gd.CompactorJamMult : 1.0) * (Has("jam_guard") ? 0.6 : 1.0);
                if (Rng.Frand() < q * accepted) Jam(i, "random");
            }
            return accepted;
        }

        void Turn(int i, int archetype)
        {
            S.Points[i].Turned++;
            S.Today.Turned++;
            Emit("turned", i, archetype);
        }

        /// <summary>Random breakdowns / jams only after the guide and the forgiving first days.</summary>
        bool JamAllowed => !S.Tutorial && S.Day > Gd.EventFreeDays;

        public void Jam(int i, string why)
        {
            var p = S.Points[i];
            if (p.Jammed) return;
            p.Jammed = true;
            p.TechCalled = false;
            Emit("jam", i, -1, 0, why);
        }
    }
}
