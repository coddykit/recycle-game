// Grill Convoy sim core — engine independent (no UnityEngine). C# 9 / .NET Standard 2.1.
// One career: a burger-truck business in the harbour city of Cinder Bay. A service day runs 10:00-20:00 game time
// (~4 real minutes at 1x):
//   plan   (10:00, paused) — which truck goes to which pitch, the pack list, crew, menu board, the truck layout;
//   live                   — trucks drive the road graph to their pitches, open the window, the crowd queues, the line
//                            (grill -> assembly bench -> window) turns tickets into burgers; restock runs, crises;
//   report (20:00)         — trucks come home, leftovers go back to the commissary (spoilage = waste), wages / permits /
//                            propane are paid, 3 × 3 stars, Fame and pitch reputation move; NextDay().
// Partial class: this file = construction, time, queries, weather; Fleet.cs = driving, dispatch, restock runs;
// Service.cs = layout, throughput, customers, the line, the guide's hand-cooked ticket; Commissary.cs = prep + loading;
// Economy.cs = day end, purchases, crew / menu / layout actions, research, offline, rewards; Crises.cs = events.
using System;
using System.Collections.Generic;

namespace GrillConvoy.Sim
{
    public sealed class SimEvent
    {
        public string Type;     // served, walked, sold_out, arrived, home, restocked, prep_done, crisis, flare, research_done, district, day_done, ...
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
        /// <summary>Emit per-customer events for the diorama's crowd (off in balance bots).</summary>
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
            S.Fame = Gd.StartFame;
            foreach (var d in Gd.Districts) if (d.Chapter <= 1 && d.Cost <= 0) S.Districts.Add(d.Id);
            foreach (int n in Gd.Pitches) S.Pitches.Add(new PitchState { Node = n, Rep = Gd.RepStart });
            for (int i = 0; i < Stock.N; i++) S.Commissary.Stock[i] = Gd.StartCommissary[i];
            for (int i = 0; i < Stock.N; i++) S.Commissary.Par[i] = Gd.StockDefs[i].Batch * 3;
            var t = NewTruck(0, Gd.StartTruck);
            t.Crew.AddRange(Gd.StartCrew);
            t.Menu.Clear();
            t.Menu.AddRange(Gd.StartMenu);
            t.Layout.Clear();
            foreach (var l in Gd.StartLayout) t.Layout.Add(new StationTile { Type = l.type, X = l.x, Y = l.y });
            for (int i = 0; i < Stock.N; i++) { t.Load[i] = Gd.StartLoad[i]; t.Pack[i] = Gd.StartLoad[i]; }
            // the first morning: truck #1 already stands at the Office Plaza, window closed (guide step 1 opens it)
            t.Pitch = Gd.Node(Gd.StartPitch);
            t.AtNode = t.Pitch;
            t.Status = TruckStatus.Service;
            S.Trucks.Add(t);
            S.Weather = "sunny";
            S.Forecast = "sunny";
            S.Today = new DayStats { Day = 1, Weather = S.Weather };
        }

        TruckState NewTruck(int id, string type)
        {
            var t = new TruckState { Id = id, Type = type, AtNode = Gd.DepotNode };
            t.Menu.AddRange(Gd.StartMenu);
            // a new truck comes with the basic line: grill, assembly bench, window
            t.Layout.Add(new StationTile { Type = Stations.Window, X = 1, Y = 0 });
            t.Layout.Add(new StationTile { Type = Stations.Assembly, X = 1, Y = 1 });
            t.Layout.Add(new StationTile { Type = Stations.Grill, X = 1, Y = 2 });
            return t;
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
        public VehicleDef VehicleOf(TruckState t) => Gd.Vehicles.TryGetValue(t.Type, out var v) ? v : Gd.Vehicles[Gd.StartTruck];
        public NodeDef NodeAt(int node) => Gd.Nodes[Math.Max(0, Math.Min(Gd.Nodes.Count - 1, node))];
        public bool Has(string research) => S.Research.Contains(research);
        public bool Unlocked(string district) => S.Districts.Contains(district);
        public bool PitchOpen(int node) => node >= 0 && node < Gd.Nodes.Count && Gd.Nodes[node].IsPitch && Unlocked(Gd.Nodes[node].District);
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
        public int HourIndex => Math.Max(0, Math.Min((Gd.DayEnd - Gd.DayStart) / 60 - 1, (int)((S.Minute - Gd.DayStart) / 60.0)));
        public bool WeekendDay(int day) => (day - 1) % 7 >= 5;
        public bool Weekend => WeekendDay(S.Day);
        /// <summary>Match days are on the calendar: every Nth day (shown 48 h ahead).</summary>
        public bool MatchDay(int day) => Gd.MatchEvery > 0 && day % Gd.MatchEvery == 0;
        public PitchState PitchState(int node)
        {
            foreach (var p in S.Pitches) if (p.Node == node) return p;
            var n = new PitchState { Node = node, Rep = Gd.RepStart };
            S.Pitches.Add(n);
            return n;
        }
        /// <summary>0 bronze, 1 silver, 2 gold.</summary>
        public int Medal(int node) { double r = PitchState(node).Rep; return r >= 70 ? 2 : r >= 40 ? 1 : 0; }
        public int TrucksAt(int node) { int n = 0; foreach (var t in S.Trucks) if (t.Open && t.AtNode == node) n++; return n; }
        public int TruckIndex(TruckState t) => S.Trucks.IndexOf(t);

        // ------------------------------------------------------------------------------------------------ time

        public void SetSpeed(int speed) => S.Speed = Math.Max(1, Math.Min(MaxSpeed, speed));
        public void SetPaused(bool paused) => S.Paused = paused;

        /// <summary>Store perks (never saved): convoy_pass, automation, offline_plus, speed, slots.</summary>
        public void SetPerks(Dictionary<string, bool> p)
        {
            perks.Clear();
            if (p != null) foreach (var kv in p) perks[kv.Key] = kv.Value;
            if (S.Speed > MaxSpeed) S.Speed = MaxSpeed;
            int slots = RuleSlots;
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
            AdvanceMinutes(realDt * Gd.GameMinPerRealS * Math.Max(1, S.Speed));
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
            StepTrucks(dt);
            StepService(dt);
            StepCommissary(dt);
            S.Minute += dt;
            if (S.Minute >= Gd.DayEnd - 1e-9) EndDay();
        }

        int Sample(double expected)
        {
            if (expected <= 0) return 0;
            int k = (int)Math.Floor(expected);
            if (Rng.Frand() < expected - k) k++;
            return k;
        }

        // ------------------------------------------------------------------------------------------------ weather

        string RollWeather(int day)
        {
            var w = new double[Gd.WeatherIds.Length];
            for (int i = 0; i < w.Length; i++)
            {
                int min = i < Gd.WeatherMinDay.Length ? Gd.WeatherMinDay[i] : 1;
                bool ok = day >= min && day > Gd.EventFreeDays || min <= 1;
                w[i] = ok && i < Gd.WeatherWeights.Length ? Gd.WeatherWeights[i] : 0;
            }
            int k = Rng.PickWeighted(w);
            return k < 0 ? "sunny" : Gd.WeatherIds[k];
        }

        /// <summary>Weather multiplier on a pitch today (sunny / cloudy / hot / rain, per pitch sensitivity).</summary>
        public double WeatherMult(int node) => NodeAt(node).WeatherMult(S.Weather);
    }
}
