// Ridgeback Rangers sim core — engine independent (no UnityEngine). C# 9 / .NET Standard 2.1.
// One career: the Chief Warden of Ridgeback Isle. A Park Day runs 08:00-18:00 game time (~4.5 real minutes at 1x).
//   plan   (08:00, paused) — feed board, tram timetable, purchases, hatchery;
//   live                   — visitors arrive at the gate and ride the safari tram past the paddocks; animals live on a hunger
//                            and mood clock; problems appear as PINS that the player answers with scarce ranger teams;
//   report (18:00)         — Sanctuary Rating stars (care, visitors, calm), coins, Conservation Points; NextDay().
// Partial class: this file = construction, queries, time, visitors + tram; Animals.cs = feeding, mood, harmony, hatchery,
// dig sites; Rangers.cs = pins + ranger teams; Economy.cs = day end, purchases, offline, rewards; Crises.cs = events.
using System;
using System.Collections.Generic;

namespace RidgebackRangers.Sim
{
    public sealed class SimEvent
    {
        public string Type;     // arrive, pin, pin_done, pin_failed, fed, ride_done, hatched, egg, dig_done, crisis, zone, day_done, day_start, ...
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
        /// <summary>Emit arrival events for the diorama's walkers (off in balance bots).</summary>
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
            S.Cp = Gd.StartCp;
            S.Rating = Gd.StartRating;
            S.Incubators = Gd.StartIncubators;
            S.Tram = new TramState { Cars = Math.Max(1, Gd.StartTramCars) };
            foreach (var z in Gd.StartZones) UnlockZone(z, false);
            foreach (var kv in Gd.StartPaddocks)
            {
                S.Paddocks.Add(new PaddockState { Node = Gd.Node(kv.Key) });
                foreach (var sp in kv.Value) AddAnimal(sp, S.Paddocks.Count - 1, false).Hunger = Gd.StartHunger;
            }
            foreach (var r in Roles.All)
                for (int i = 0; i < (Gd.StartRangers.TryGetValue(r, out int n) ? n : 0); i++) AddRanger(r, false);
            S.Today = new DayStats { Day = 1 };
        }

        void UnlockZone(string id, bool emit)
        {
            if (S.Zones.Contains(id)) return;
            S.Zones.Add(id);
            if (!S.Stations.ContainsKey(id)) S.Stations[id] = new StationState { Plan = Gd.StartFeedPlan };
            if (emit) Emit("zone", -1, -1, 0, id);
        }

        public AnimalState AddAnimal(string species, int paddock, bool young)
        {
            var a = new AnimalState { Id = S.NextId++, Species = species, Paddock = paddock, Young = young, Mood = 0.65 };
            S.Animals.Add(a);
            if (!S.Found.Contains(species)) S.Found.Add(species);
            return a;
        }

        RangerState AddRanger(string role, bool temp)
        {
            var r = new RangerState { Id = S.NextId++, Role = role, AtNode = Gd.HqNode, Temp = temp };
            S.Rangers.Add(r);
            return r;
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
        public bool Has(string upgrade) => S.Upgrades.Contains(upgrade);
        public bool Unlocked(string zone) => S.Zones.Contains(zone);
        public bool Perk(string id) => perks.TryGetValue(id, out bool b) && b;
        public int Chapter
        {
            get
            {
                int c = 1;
                foreach (var z in Gd.Zones) if (Unlocked(z.Id)) c = Math.Max(c, z.Chapter);
                return c;
            }
        }
        public int MaxSpeed => Perk("speed") ? Gd.PaidMaxSpeed : Gd.FreeMaxSpeed;
        public int HourIndex => Math.Max(0, Math.Min(Gd.HourCurve.Length - 1, (int)((S.Minute - Gd.DayStart) / 60.0)));
        public NodeDef NodeOf(PaddockState p) => Gd.Nodes[p.Node];
        public string ZoneOf(PaddockState p) => Gd.Nodes[p.Node].Zone;
        public string BiomeOf(PaddockState p) => Gd.Zone(ZoneOf(p))?.Biome ?? "forest";
        public int PaddockAt(int node) => S.Paddocks.FindIndex(p => p.Node == node);
        public SpeciesDef SpeciesOf(AnimalState a) => Gd.Sp(a.Species) ?? Gd.Species[0];
        public int AnimalIndex(int id) => S.Animals.FindIndex(a => a.Id == id);
        public bool EventActive(string id) => S.Events.Exists(e => e.Id == id && (e.Until <= 0 || S.Minute < e.Until));
        public double EventMult(string id) { var e = S.Events.Find(x => x.Id == id && (x.Until <= 0 || S.Minute < x.Until)); return e != null ? e.Mult : 1.0; }
        public int RangerCap => Perk("ranger_pass") ? Gd.RangerCapPass : Gd.RangerCap;
        public int IncubatorCap => Gd.IncubatorMax + (Perk("ranger_pass") ? 1 : 0);
        public int Queued { get { int n = 0; foreach (var g in S.Queue) n += g.N; return n; } }
        public int Seats => (int)Math.Round(Gd.TramSeats) * Math.Max(1, S.Tram.Cars);
        public bool GateClosedForDay => S.Minute >= Gd.DayEnd - Gd.GateCloseBeforeEnd;
        public double TicketPrice => Gd.Ticket * (Gd.TicketRatingMin + Gd.TicketRatingSpan * Math.Max(0, Math.Min(100, S.Rating)) / 100.0);

        // ------------------------------------------------------------------------------------------------ time

        public void SetSpeed(int speed) => S.Speed = Math.Max(1, Math.Min(MaxSpeed, speed));
        public void SetPaused(bool paused) => S.Paused = paused;

        /// <summary>Store perks (never saved): ranger_pass, offline_plus, speed, premium (golden tram).</summary>
        public void SetPerks(Dictionary<string, bool> p)
        {
            perks.Clear();
            if (p != null) foreach (var kv in p) perks[kv.Key] = kv.Value;
            if (S.Speed > MaxSpeed) S.Speed = MaxSpeed;
        }

        /// <summary>Every frame with real seconds. Eggs warm on real time in every phase; the day only runs while live.</summary>
        public void Advance(double realDt)
        {
            if (realDt <= 0 || double.IsNaN(realDt)) return;
            realDt = Math.Min(realDt, 1.0);   // a hitch never jumps the sim
            TickEggs(realDt);
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
            ExpireEvents();
            StepAnimals(dt);
            StepPins(dt);
            StepRangers(dt);
            Visitors(dt);
            StepTram(dt);
            if (S.Minute < Gd.Midday && S.Minute + dt >= Gd.Midday && (Has("feed_run2") || EventActive("supply_late"))) DeliverFeed(true);
            if (Math.Floor((S.Minute + dt) / 30.0) > Math.Floor(S.Minute / 30.0)) SampleCare();   // every half game hour (save-stable)
            S.Minute += dt;
            if (S.Minute >= Gd.DayEnd - 1e-9) EndDay();
        }

        // ------------------------------------------------------------------------------------------------ visitors

        int Sample(double expected)
        {
            if (expected <= 0) return 0;
            int k = (int)Math.Floor(expected);
            if (Rng.Frand() < expected - k) k++;
            return k;
        }

        /// <summary>Total park appeal now (sum of the zone viewpoints' appeal).</summary>
        public double ParkAppeal()
        {
            double a = 0;
            foreach (var z in S.Zones) a += ZoneAppeal(z);
            return a;
        }

        /// <summary>Visitors per game hour arriving at the gate right now (before randomness).</summary>
        public double ArrivalRate()
        {
            double rating = Gd.RatingMin + Gd.RatingSpan * Math.Max(0, Math.Min(100, S.Rating)) / 100.0;
            double appeal = Math.Max(Gd.AppealBase, Math.Min(Gd.AppealMax, Gd.AppealBase + ParkAppeal() / Math.Max(1, Gd.AppealDiv)));
            double ev = EventMult("school_trip") * (EventActive("rain_squall") ? EventMult("rain_squall") : 1.0);
            return Gd.VisitorsPerH * Gd.HourCurve[HourIndex] * rating * appeal * ev;
        }

        void Visitors(double dt)
        {
            // patience: groups that waited too long walk the paths instead (smaller joy, low satisfaction)
            while (S.Queue.Count > 0 && S.Minute - S.Queue[0].At > Gd.Patience)
            {
                Walk(S.Queue[0].N);
                S.Queue.RemoveAt(0);
            }
            if (!S.GateOpen || GateClosedForDay) return;
            int n = Sample(ArrivalRate() * dt / 60.0);
            if (n <= 0) return;
            double ticket = TicketPrice;
            S.Today.Visitors += n;
            S.VisitorsTotal += n;
            S.Today.Tickets += n * ticket;
            S.Cash += n * ticket;
            S.Queue.Add(new VisitorGroup { N = n, At = S.Minute });
            if (EmitCrowd) Emit("arrive", n);
        }

        void Walk(int n)
        {
            if (n <= 0) return;
            double joy = ZoneAppeal(Gd.StartZones.Count > 0 ? Gd.StartZones[0] : "cove") * Gd.WalkJoy;
            S.Today.Walked += n;
            S.Today.Joy += joy * n;
            S.Today.SatSum += Gd.WalkSat * n;
            S.Today.SatN += n;
            double snacks = n * Gd.Snack * 0.5 * (1 + S.Cafe * Gd.CafePerLevel);
            S.Today.Snacks += snacks;
            S.Cash += snacks;
            Emit("walked", n);
        }

        // ------------------------------------------------------------------------------------------------ tram

        /// <summary>The loop's stop nodes: the station, every unlocked zone's viewpoint, back to the station (implicit).</summary>
        public List<int> TramStops()
        {
            var l = new List<int> { Gd.StationNode };
            foreach (var z in Gd.Zones) if (Unlocked(z.Id)) { int v = Gd.ViewOf(z.Id); if (v >= 0) l.Add(v); }
            return l;
        }

        static double Dist(NodeDef a, NodeDef b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

        /// <summary>Cumulative km at each stop (index 0 = 0) and the loop length (last entry = back at the station).</summary>
        public List<double> TramMarks()
        {
            var stops = TramStops();
            var marks = new List<double> { 0 };
            double km = 0;
            for (int i = 1; i <= stops.Count; i++)
            {
                km += Dist(Gd.Nodes[stops[i - 1]], Gd.Nodes[stops[i % stops.Count]]) * Gd.RoadFactor;
                marks.Add(km);
            }
            return marks;
        }

        public double TramLoopKm { get { var m = TramMarks(); return m[m.Count - 1]; } }
        public bool TramHalted => S.Pins.Exists(p => p.Kind == PinKinds.Escape);
        double TramKmh => Gd.TramKmh * (EventActive("dust_storm") ? EventMult("dust_storm") : 1.0);

        /// <summary>Minutes of one full loop (driving + dwell at every viewpoint).</summary>
        public double TramLoopMin => TramLoopKm / Math.Max(0.1, TramKmh) * 60.0 + (TramStops().Count - 1) * Gd.TramDwell;

        /// <summary>Seats per hour the tram can carry (cars × seats / loop time).</summary>
        public double TramCapacityPerH => Seats * 60.0 / Math.Max(5, TramLoopMin + 2);

        /// <summary>A zone's stop is closed while its path is flooded.</summary>
        public bool StopOpen(string zone) => !S.Pins.Exists(p => p.Kind == PinKinds.Flood && p.Zone == zone);

        void Board()
        {
            var t = S.Tram;
            int free = Seats - t.Riders;
            while (free > 0 && S.Queue.Count > 0)
            {
                var g = S.Queue[0];
                int take = Math.Min(free, g.N);
                t.Riders += take;
                t.Wait += take * Math.Max(0, S.Minute - g.At);
                free -= take;
                g.N -= take;
                if (g.N <= 0) S.Queue.RemoveAt(0);
            }
        }

        /// <summary>Sends the tram out now (the player's tap). "" = ok.</summary>
        public string TramGo()
        {
            if (S.Phase != Phases.Live) return "phase";
            var t = S.Tram;
            if (t.Running) return "running";
            if (TramHalted) return "halted";
            Board();
            if (t.Riders <= 0) return "empty";
            Depart();
            return "";
        }

        void Depart()
        {
            var t = S.Tram;
            t.Running = true;
            t.Pos = 0;
            t.NextStop = 1;
            t.Dwell = 0;
            Emit("tram_go", t.Riders);
        }

        public string SetTramPolicy(string policy, int interval)
        {
            if (!TramPolicy.Valid(policy)) return "invalid";
            S.Tram.Policy = policy;
            if (policy == TramPolicy.Interval) S.Tram.Interval = Array.IndexOf(TramPolicy.Intervals, interval) >= 0 ? interval : 20;
            Emit("tram_policy", -1, -1, interval, policy);
            return "";
        }

        void StepTram(double dt)
        {
            var t = S.Tram;
            if (!t.Running)
            {
                Board();
                if (S.Tutorial || TramHalted || t.Riders <= 0) { if (t.Riders <= 0) t.BoardSince = S.Minute; return; }
                double waited = S.Minute - t.BoardSince;
                bool go = t.Policy == TramPolicy.Full ? t.Riders >= Seats || waited >= Gd.TramMaxWait : waited >= t.Interval;
                if (go) Depart();
                return;
            }
            if (TramHalted) return;
            if (t.Dwell > 0) { t.Dwell = Math.Max(0, t.Dwell - dt); return; }
            var marks = TramMarks();
            var stops = TramStops();
            if (t.NextStop >= marks.Count) t.NextStop = marks.Count - 1;
            t.Pos += TramKmh * dt / 60.0;
            if (t.Pos + 1e-9 < marks[t.NextStop]) return;
            t.Pos = marks[t.NextStop];
            if (t.NextStop < stops.Count)
            {
                // a viewpoint: the riders watch the paddocks (joy), a short dwell
                string zone = Gd.Nodes[stops[t.NextStop]].Zone;
                if (StopOpen(zone)) t.Joy += t.Riders * ZoneAppeal(zone);
                t.Dwell = Gd.TramDwell;
                t.NextStop++;
                return;
            }
            Arrive();
        }

        void Arrive()
        {
            var t = S.Tram;
            int n = t.Riders;
            if (n > 0)
            {
                double joyEach = t.Joy / n;
                double waitEach = t.Wait / n;
                double sat = Math.Max(0, Math.Min(1, joyEach / Math.Max(1, Gd.JoyTarget))) * (1 - 0.3 * Math.Min(1, waitEach / Math.Max(1, Gd.Patience)));
                double snacks = n * Gd.Snack * (1 + S.Cafe * Gd.CafePerLevel);
                double gifts = Has("gift") ? n * Gd.Gift * (0.5 + sat) : 0;
                double don = n * Gd.Donation * sat;
                double cp = t.Joy * Gd.CpPerJoy;
                S.Today.Rides += n;
                S.Today.Joy += t.Joy;
                S.Today.SatSum += sat * n;
                S.Today.SatN += n;
                S.Today.Snacks += snacks;
                S.Today.Gifts += gifts;
                S.Today.Donations += don;
                S.Today.CpEarned += cp;
                S.Cash += snacks + gifts + don;
                S.Cp += cp;
                Emit("ride_done", n, -1, sat);
            }
            t.Running = false;
            t.Riders = 0;
            t.Joy = 0;
            t.Wait = 0;
            t.Pos = 0;
            t.NextStop = 1;
            t.Dwell = 0;
            t.BoardSince = S.Minute;
        }

        /// <summary>Tram position for the diorama: (x, y, heading) in map km along the stop polyline.</summary>
        public (double x, double y, double heading) TramPoint(double km)
        {
            var stops = TramStops();
            var marks = TramMarks();
            double loop = marks[marks.Count - 1];
            if (loop <= 0) { var st = Gd.Nodes[Gd.StationNode]; return (st.X, st.Y, 0); }
            km = ((km % loop) + loop) % loop;
            for (int i = 1; i < marks.Count; i++)
            {
                if (km > marks[i] && i < marks.Count - 1) continue;
                var a = Gd.Nodes[stops[i - 1]];
                var b = Gd.Nodes[stops[i % stops.Count]];
                double seg = marks[i] - marks[i - 1];
                double f = seg <= 0 ? 0 : Math.Max(0, Math.Min(1, (km - marks[i - 1]) / seg));
                return (a.X + (b.X - a.X) * f, a.Y + (b.Y - a.Y) * f, Math.Atan2(b.X - a.X, b.Y - a.Y));
            }
            var s0 = Gd.Nodes[Gd.StationNode];
            return (s0.X, s0.Y, 0);
        }
    }
}
