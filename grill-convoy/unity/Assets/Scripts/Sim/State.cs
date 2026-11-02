// Grill Convoy sim core — engine independent. The whole saved career (State) and its parts. Every class writes and reads
// itself (ToJson / Read); reads are lenient (defaults for missing fields) so older saves load (fleet §14: the codec accepts
// every state the sim can produce — negative cash, a truck mid-road or mid-restock, a queue at the window, a flare-up, the
// report screen, a hand-cooked guide ticket).
using System;
using System.Collections.Generic;

namespace GrillConvoy.Sim
{
    public static class Phases
    {
        public const string Plan = "plan", Live = "live", Report = "report";
        public static bool Valid(string p) => p == Plan || p == Live || p == Report;
    }

    public static class TruckStatus
    {
        /// <summary>idle = parked at the commissary; drive = on the road (to its pitch, or home when Target = -1);
        /// service = open at a pitch; restock = loading at the commissary mid-day, then back to the pitch.</summary>
        public const string Idle = "idle", Drive = "drive", Service = "service", Restock = "restock";
        public static bool Valid(string s) => s == Idle || s == Drive || s == Service || s == Restock;
    }

    static class JW
    {
        public static List<object> Arr(double[] a) { var l = new List<object>(); foreach (var x in a) l.Add(x); return l; }
        public static List<object> Arr(IEnumerable<int> a) { var l = new List<object>(); foreach (var x in a) l.Add((long)x); return l; }
        public static List<object> Arr(IEnumerable<string> a) { var l = new List<object>(); foreach (var x in a) l.Add(x); return l; }
        public static double[] Dbl(object o, string key, int len)
        {
            var r = new double[len];
            var a = J.Arr(o, key);
            if (a != null) for (int i = 0; i < len && i < a.Count; i++) r[i] = Finite(J.D(a[i]));
            return r;
        }
        public static int[] IntN(object o, string key, int len)
        {
            var r = new int[len];
            var a = J.Arr(o, key);
            if (a != null) for (int i = 0; i < len && i < a.Count; i++) r[i] = J.I(a[i]);
            return r;
        }
        public static List<int> Ints(object o, string key) { var r = new List<int>(); foreach (var x in J.Arr(o, key) ?? new List<object>()) r.Add(J.I(x)); return r; }
        public static double Finite(double d) => double.IsNaN(d) || double.IsInfinity(d) ? 0 : d;
        public static double D(object o, string key, double def = 0) => Finite(J.D(o, key, def));
    }

    public sealed class StationTile
    {
        public string Type = Stations.Grill;
        public int X, Y;
        public StationTile Clone() => new StationTile { Type = Type, X = X, Y = Y };
        public JObj ToJson() => new JObj { ["t"] = Type, ["x"] = (long)X, ["y"] = (long)Y };
        public static StationTile Read(object o) => new StationTile { Type = J.S(o, "t", Stations.Grill), X = J.I(o, "x"), Y = J.I(o, "y") };
        public static List<StationTile> ReadList(object o, string key)
        {
            var l = new List<StationTile>();
            foreach (var x in J.Arr(o, key) ?? new List<object>()) l.Add(Read(x));
            return l;
        }
        public static List<object> WriteList(List<StationTile> l)
        {
            var a = new List<object>();
            if (l != null) foreach (var s in l) a.Add(s.ToJson());
            return a;
        }
        public static List<StationTile> CloneList(List<StationTile> l)
        {
            var c = new List<StationTile>();
            if (l != null) foreach (var s in l) c.Add(s.Clone());
            return c;
        }
    }

    /// <summary>One customer in a truck's line (or an order-ahead ticket).</summary>
    public sealed class Customer
    {
        public int Arch, Card, Items = 1;
        public bool Side, Ahead;
        public double At, Patience = 8;
        public JObj ToJson() => new JObj { ["a"] = (long)Arch, ["c"] = (long)Card, ["n"] = (long)Items, ["s"] = Side, ["o"] = Ahead, ["at"] = At, ["p"] = Patience };
        public static Customer Read(object o) => new Customer
        {
            Arch = J.I(o, "a"), Card = J.I(o, "c"), Items = Math.Max(1, J.I(o, "n", 1)), Side = J.B(o, "s"), Ahead = J.B(o, "o"), At = JW.D(o, "at"), Patience = JW.D(o, "p", 8),
        };
    }

    public sealed class TruckState
    {
        public int Id;
        public string Type = "starter";
        public string Status = TruckStatus.Idle;
        public int Pitch = -1;                        // assigned pitch node (-1 = none)
        public List<int> Path = new List<int>();      // node path of the current leg
        public double PathKm, PathPos;                // leg length / progress (km)
        public int Target = -1;                       // node the leg goes to (-1 = the commissary)
        public int AtNode;                            // where it stands when not driving
        public bool Restocking;                       // heading home for a restock run (comes back to its pitch)
        public double TimeLeft;                       // minutes of restock loading
        public int[] Load = new int[Stock.N];         // stock on board
        public int[] Pack = new int[Stock.N];         // the last departure load ("pack like yesterday")
        public List<string> Crew = new List<string>();
        public List<string> Menu = new List<string>();
        public int Tier = 1;                          // fixings tier 0 budget, 1 standard, 2 premium
        public bool Extended;                         // body extension (bigger grid)
        public List<StationTile> Layout = new List<StationTile>();
        public List<StationTile> Undo;                // one step of layout undo (null = nothing)
        public List<List<StationTile>> Presets = new List<List<StationTile>>();
        public List<Customer> Queue = new List<Customer>();
        public double Work;                           // burger work done toward the head order
        public double Grease;                         // patties since the last clean
        public double Propane = 1;                    // tank 0..1
        public double FlareUntil;                     // day minute a flare-up burns out by itself (0 = none)
        public double GrillOffUntil;                  // clean cycle (grill offline)
        public double AllHandsUntil, AllHandsReady;   // burst window / cooldown (day minutes)
        public double HelperUntil;                    // rewarded extra runner
        public bool Promo;                            // today's rival counter-offer
        public bool Auto;                             // Dispatcher / Head cook rules drive it
        public double KmToday;
        // today
        public int Served, Walked, SoldOut, Arrivals, Items;
        public double SelloutMin, OpenMin, WaitSum, Sales;

        public bool Busy => Status != TruckStatus.Idle;
        public bool Open => Status == TruckStatus.Service;
        public bool Flaring => FlareUntil > 0;

        public JObj ToJson()
        {
            var pr = new List<object>();
            foreach (var p in Presets) pr.Add(StationTile.WriteList(p));
            var q = new List<object>();
            foreach (var c in Queue) q.Add(c.ToJson());
            return new JObj
            {
                ["id"] = (long)Id, ["type"] = Type, ["status"] = Status, ["pitch"] = (long)Pitch, ["path"] = JW.Arr(Path), ["path_km"] = PathKm, ["pos"] = PathPos,
                ["target"] = (long)Target, ["at"] = (long)AtNode, ["restocking"] = Restocking, ["left"] = TimeLeft, ["load"] = JW.Arr(Load), ["pack"] = JW.Arr(Pack),
                ["crew"] = JW.Arr(Crew), ["menu"] = JW.Arr(Menu), ["tier"] = (long)Tier, ["ext"] = Extended, ["layout"] = StationTile.WriteList(Layout),
                ["undo"] = Undo == null ? null : StationTile.WriteList(Undo), ["presets"] = pr, ["queue"] = q, ["work"] = Work, ["grease"] = Grease,
                ["propane"] = Propane, ["flare"] = FlareUntil, ["grill_off"] = GrillOffUntil, ["hands"] = AllHandsUntil, ["hands_ready"] = AllHandsReady,
                ["helper"] = HelperUntil, ["promo"] = Promo, ["auto"] = Auto, ["km"] = KmToday, ["served"] = (long)Served, ["walked"] = (long)Walked,
                ["sold_out"] = (long)SoldOut, ["arr"] = (long)Arrivals, ["items"] = (long)Items, ["sellout_min"] = SelloutMin, ["open_min"] = OpenMin,
                ["wait"] = WaitSum, ["sales"] = Sales,
            };
        }

        public static TruckState Read(object o)
        {
            var t = new TruckState
            {
                Id = J.I(o, "id"), Type = J.S(o, "type", "starter"), Status = J.S(o, "status", TruckStatus.Idle), Pitch = J.I(o, "pitch", -1), Path = JW.Ints(o, "path"),
                PathKm = JW.D(o, "path_km"), PathPos = JW.D(o, "pos"), Target = J.I(o, "target", -1), AtNode = J.I(o, "at"), Restocking = J.B(o, "restocking"),
                TimeLeft = JW.D(o, "left"), Load = JW.IntN(o, "load", Stock.N), Pack = JW.IntN(o, "pack", Stock.N), Crew = J.StrList(o, "crew"), Menu = J.StrList(o, "menu"),
                Tier = J.I(o, "tier", 1), Extended = J.B(o, "ext"), Layout = StationTile.ReadList(o, "layout"),
                Undo = J.Arr(o, "undo") != null ? StationTile.ReadList(o, "undo") : null, Work = JW.D(o, "work"), Grease = JW.D(o, "grease"),
                Propane = JW.D(o, "propane", 1), FlareUntil = JW.D(o, "flare"), GrillOffUntil = JW.D(o, "grill_off"), AllHandsUntil = JW.D(o, "hands"),
                AllHandsReady = JW.D(o, "hands_ready"), HelperUntil = JW.D(o, "helper"), Promo = J.B(o, "promo"), Auto = J.B(o, "auto"), KmToday = JW.D(o, "km"),
                Served = J.I(o, "served"), Walked = J.I(o, "walked"), SoldOut = J.I(o, "sold_out"), Arrivals = J.I(o, "arr"), Items = J.I(o, "items"),
                SelloutMin = JW.D(o, "sellout_min"), OpenMin = JW.D(o, "open_min"), WaitSum = JW.D(o, "wait"), Sales = JW.D(o, "sales"),
            };
            foreach (var p in J.Arr(o, "presets") ?? new List<object>())
            {
                var l = new List<StationTile>();
                if (p is List<object> a) foreach (var x in a) l.Add(StationTile.Read(x));
                t.Presets.Add(l);
            }
            foreach (var c in J.Arr(o, "queue") ?? new List<object>()) t.Queue.Add(Customer.Read(c));
            if (!TruckStatus.Valid(t.Status)) t.Status = TruckStatus.Idle;
            for (int i = 0; i < Stock.N; i++) { if (t.Load[i] < 0) t.Load[i] = 0; if (t.Pack[i] < 0) t.Pack[i] = 0; }
            t.Tier = Math.Max(0, Math.Min(2, t.Tier));
            t.Propane = Math.Max(0, Math.Min(1, t.Propane));
            return t;
        }
    }

    public sealed class CommissaryState
    {
        public int[] Stock = new int[Sim.Stock.N];
        public List<string> Queue = new List<string>();   // prep batches (stock ids), head = in progress
        public double PrepLeft;                           // game minutes left on the head batch
        public int[] Par = new int[Sim.Stock.N];          // auto-prep targets (par_levels research)

        public JObj ToJson() => new JObj { ["stock"] = JW.Arr(Stock), ["queue"] = JW.Arr(Queue), ["prep_left"] = PrepLeft, ["par"] = JW.Arr(Par) };

        public static CommissaryState Read(object o)
        {
            var c = new CommissaryState { Stock = JW.IntN(o, "stock", Sim.Stock.N), Queue = J.StrList(o, "queue"), PrepLeft = JW.D(o, "prep_left"), Par = JW.IntN(o, "par", Sim.Stock.N) };
            for (int i = 0; i < Sim.Stock.N; i++) { if (c.Stock[i] < 0) c.Stock[i] = 0; if (c.Par[i] < 0) c.Par[i] = 0; }
            return c;
        }
    }

    public sealed class PitchState
    {
        public int Node;
        public double Rep = 30;     // pitch reputation 0..100 (bronze / silver / gold)
        public int Served;          // career total
        public JObj ToJson() => new JObj { ["node"] = (long)Node, ["rep"] = Rep, ["served"] = (long)Served };
        public static PitchState Read(object o) => new PitchState { Node = J.I(o, "node", -1), Rep = JW.D(o, "rep", 30), Served = J.I(o, "served") };
    }

    public sealed class ActiveEvent
    {
        public string Id;
        public int UntilDay;          // last day it applies (inclusive)
        public int Target = -1;       // pitch node / truck index
        public double Mult = 1;
        public JObj ToJson() => new JObj { ["id"] = Id, ["until"] = (long)UntilDay, ["target"] = (long)Target, ["mult"] = Mult };
        public static ActiveEvent Read(object o) => new ActiveEvent { Id = J.S(o, "id"), UntilDay = J.I(o, "until"), Target = J.I(o, "target", -1), Mult = JW.D(o, "mult", 1) };
    }

    public sealed class PlannedEvent
    {
        public string Id;
        public double At;             // day minute
        public JObj ToJson() => new JObj { ["id"] = Id, ["at"] = At };
        public static PlannedEvent Read(object o) => new PlannedEvent { Id = J.S(o, "id"), At = JW.D(o, "at") };
    }

    public sealed class DayStats
    {
        public int Day;
        public int Arrivals, Served, Walked, SoldOut, Items, Sides, Waste;
        public double WaitSum, SelloutMin, OpenMin, Km, Fuel, Wages, Permits, Ingredients, Fixings, Propane, Sales, Bonus, WasteValue;
        public int StarService, StarStock, StarSpeed;
        public string Weather = "sunny";
        public double Income => Sales + Bonus;
        public double Costs => Fuel + Wages + Permits + Ingredients + Fixings + Propane;
        public double Net => Income - Costs;
        public int Stars => StarService + StarStock + StarSpeed;
        public double LostRate => Arrivals <= 0 ? 0 : (double)(Walked + SoldOut) / Arrivals;
        public double AvgWait => Served <= 0 ? 0 : WaitSum / Served;
        public double SelloutRate => OpenMin <= 0 ? 0 : SelloutMin / OpenMin;

        public JObj ToJson()
        {
            return new JObj
            {
                ["day"] = (long)Day, ["arr"] = (long)Arrivals, ["served"] = (long)Served, ["walked"] = (long)Walked, ["sold_out"] = (long)SoldOut, ["items"] = (long)Items,
                ["sides"] = (long)Sides, ["waste"] = (long)Waste, ["wait"] = WaitSum, ["sellout_min"] = SelloutMin, ["open_min"] = OpenMin, ["km"] = Km, ["fuel"] = Fuel,
                ["wages"] = Wages, ["permits"] = Permits, ["ingredients"] = Ingredients, ["fixings"] = Fixings, ["propane"] = Propane, ["sales"] = Sales,
                ["bonus"] = Bonus, ["waste_value"] = WasteValue, ["s1"] = (long)StarService, ["s2"] = (long)StarStock, ["s3"] = (long)StarSpeed, ["weather"] = Weather,
            };
        }

        public static DayStats Read(object o)
        {
            return new DayStats
            {
                Day = J.I(o, "day"), Arrivals = J.I(o, "arr"), Served = J.I(o, "served"), Walked = J.I(o, "walked"), SoldOut = J.I(o, "sold_out"), Items = J.I(o, "items"),
                Sides = J.I(o, "sides"), Waste = J.I(o, "waste"), WaitSum = JW.D(o, "wait"), SelloutMin = JW.D(o, "sellout_min"), OpenMin = JW.D(o, "open_min"),
                Km = JW.D(o, "km"), Fuel = JW.D(o, "fuel"), Wages = JW.D(o, "wages"), Permits = JW.D(o, "permits"), Ingredients = JW.D(o, "ingredients"),
                Fixings = JW.D(o, "fixings"), Propane = JW.D(o, "propane"), Sales = JW.D(o, "sales"), Bonus = JW.D(o, "bonus"), WasteValue = JW.D(o, "waste_value"),
                StarService = J.I(o, "s1"), StarStock = J.I(o, "s2"), StarSpeed = J.I(o, "s3"), Weather = J.S(o, "weather", "sunny"),
            };
        }
    }

    public sealed class RewardUse
    {
        public long UtcDay;
        public int Count;
        public long LastAt;
        public JObj ToJson() => new JObj { ["day"] = UtcDay, ["n"] = (long)Count, ["at"] = LastAt };
        public static RewardUse Read(object o) => new RewardUse { UtcDay = J.L(o, "day"), Count = J.I(o, "n"), LastAt = J.L(o, "at") };
    }

    /// <summary>The saved career.</summary>
    public sealed class State
    {
        public const int Version = 1;
        public long Seed = 1, RngState = 1;
        public int Day = 1;
        public double Minute = 600;
        public string Phase = Phases.Plan;
        public int Speed = 1;
        public bool Paused;
        public double Cash;
        public double Fame = 30;
        public List<string> Districts = new List<string>();
        public List<string> Research = new List<string>();
        public string Researching = "";
        public double ResearchLeftS;
        public List<TruckState> Trucks = new List<TruckState>();
        public CommissaryState Commissary = new CommissaryState();
        public List<PitchState> Pitches = new List<PitchState>();
        public string Weather = "sunny", Forecast = "sunny";
        public List<ActiveEvent> Events = new List<ActiveEvent>();
        public List<PlannedEvent> Planned = new List<PlannedEvent>();
        public DayStats Today = new DayStats { Day = 1 };
        public List<DayStats> History = new List<DayStats>();
        public bool Tutorial = true;      // guide running: no crises, no penalties
        public int HandStage;             // the guide's hand-cooked first ticket (0 = none, 1 ticket … 6 assembled)
        public double HandTimer;          // game minutes left on the hand-cooked patty
        public int PropaneKits;           // spare bottles from the Propane Rescue Kit (free swaps)
        public Dictionary<string, RewardUse> Rewards = new Dictionary<string, RewardUse>();
        public double OfflinePending;     // offline pay shown but not doubled yet (offline_x2)
        public long LastSeen;             // unix seconds of the last save
        public int BestDayStars, ThreeStarDays;
        public long Burgers;              // career total served
        public List<string> Seen = new List<string>();

        public JObj ToJson()
        {
            var trs = new List<object>(); foreach (var t in Trucks) trs.Add(t.ToJson());
            var ps = new List<object>(); foreach (var p in Pitches) ps.Add(p.ToJson());
            var ev = new List<object>(); foreach (var e in Events) ev.Add(e.ToJson());
            var pl = new List<object>(); foreach (var e in Planned) pl.Add(e.ToJson());
            var hi = new List<object>(); foreach (var h in History) hi.Add(h.ToJson());
            var rw = new JObj(); foreach (var kv in Rewards) rw[kv.Key] = kv.Value.ToJson();
            return new JObj
            {
                ["game"] = "grill-convoy", ["v"] = (long)Version, ["seed"] = Seed, ["rng"] = RngState, ["day"] = (long)Day, ["minute"] = Minute, ["phase"] = Phase,
                ["speed"] = (long)Speed, ["paused"] = Paused, ["cash"] = Cash, ["fame"] = Fame, ["districts"] = JW.Arr(Districts), ["research"] = JW.Arr(Research),
                ["researching"] = Researching, ["research_left"] = ResearchLeftS, ["trucks"] = trs, ["commissary"] = Commissary.ToJson(), ["pitches"] = ps,
                ["weather"] = Weather, ["forecast"] = Forecast, ["events"] = ev, ["planned"] = pl, ["today"] = Today.ToJson(), ["history"] = hi,
                ["tutorial"] = Tutorial, ["hand"] = (long)HandStage, ["hand_timer"] = HandTimer, ["kits"] = (long)PropaneKits, ["rewards"] = rw,
                ["offline_pending"] = OfflinePending, ["last_seen"] = LastSeen, ["best_stars"] = (long)BestDayStars, ["three_star_days"] = (long)ThreeStarDays,
                ["burgers"] = Burgers, ["seen"] = JW.Arr(Seen),
            };
        }
    }
}
