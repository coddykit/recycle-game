// Bottle Route sim core — engine independent. The whole saved career (State) and its parts. Every class writes and reads
// itself (ToJson / Read); reads are lenient (defaults for missing fields) so older saves load (fleet §14: the codec accepts
// every state the sim can produce — negative cash, a truck mid-road, a jammed machine, the report screen, a tutorial step).
using System;
using System.Collections.Generic;

namespace BottleRoute.Sim
{
    public static class Phases
    {
        public const string Plan = "plan", Live = "live", Report = "report";
        public static bool Valid(string p) => p == Plan || p == Live || p == Report;
    }

    public static class Policies
    {
        public const string Threshold = "threshold", Fixed = "fixed", Call = "call";
        public static bool Valid(string p) => p == Threshold || p == Fixed || p == Call;
        public static readonly int[] Thresholds = { 60, 80, 95 };
        public static readonly int[] FixedDays = { 1, 2, 3 };
    }

    public static class TruckStatus
    {
        public const string Idle = "idle", Drive = "drive", Service = "service", Unload = "unload", Wait = "wait", Broken = "broken";
        public static bool Valid(string s) => s == Idle || s == Drive || s == Service || s == Unload || s == Wait || s == Broken;
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
        public static List<int> Ints(object o, string key) { var r = new List<int>(); foreach (var x in J.Arr(o, key) ?? new List<object>()) r.Add(J.I(x)); return r; }
        public static double Finite(double d) => double.IsNaN(d) || double.IsInfinity(d) ? 0 : d;
        public static double D(object o, string key, double def = 0) => Finite(J.D(o, key, def));
    }

    public sealed class PointState
    {
        public int Node;
        public string Kind = "machine_s";      // machine_s | machine_m | shop | horeca
        public double[] Fill = new double[Mats.N]; // containers by material
        public bool Sensor, Compactor, Jammed, SensorOffline;
        public double FrozenPct;                // what the screen shows while the sensor/app is wrong
        public string Policy = Policies.Threshold;
        public int Threshold = 80, FixedEvery = 1;
        public int LastServiceDay = -1;
        public double LastServiceMin;
        public double EstVol;                   // expected litres since the last service (no events, no weekend): the estimate without a sensor
        public double BusyUntil;                // bulk returner blocking the machine (day minute)
        public bool TechCalled;
        // today
        public double FullMin;
        public int Arrivals, Turned, Deposited, Picked;

        public bool IsMachine => Kind == "machine_s" || Kind == "machine_m";

        public JObj ToJson()
        {
            return new JObj
            {
                ["node"] = (long)Node, ["kind"] = Kind, ["fill"] = JW.Arr(Fill), ["sensor"] = Sensor, ["compactor"] = Compactor,
                ["jammed"] = Jammed, ["sensor_off"] = SensorOffline, ["frozen"] = FrozenPct, ["policy"] = Policy, ["threshold"] = (long)Threshold,
                ["every"] = (long)FixedEvery, ["last_day"] = (long)LastServiceDay, ["last_min"] = LastServiceMin, ["est"] = EstVol, ["busy"] = BusyUntil,
                ["tech"] = TechCalled, ["full_min"] = FullMin, ["arr"] = (long)Arrivals, ["turned"] = (long)Turned, ["dep"] = (long)Deposited, ["picked"] = (long)Picked,
            };
        }

        public static PointState Read(object o)
        {
            var p = new PointState
            {
                Node = J.I(o, "node", -1), Kind = J.S(o, "kind", "machine_s"), Fill = JW.Dbl(o, "fill", Mats.N), Sensor = J.B(o, "sensor"),
                Compactor = J.B(o, "compactor"), Jammed = J.B(o, "jammed"), SensorOffline = J.B(o, "sensor_off"), FrozenPct = JW.D(o, "frozen"),
                Policy = J.S(o, "policy", Policies.Threshold), Threshold = J.I(o, "threshold", 80), FixedEvery = J.I(o, "every", 1),
                LastServiceDay = J.I(o, "last_day", -1), LastServiceMin = JW.D(o, "last_min"), EstVol = JW.D(o, "est"), BusyUntil = JW.D(o, "busy"),
                TechCalled = J.B(o, "tech"), FullMin = JW.D(o, "full_min"), Arrivals = J.I(o, "arr"), Turned = J.I(o, "turned"),
                Deposited = J.I(o, "dep"), Picked = J.I(o, "picked"),
            };
            for (int i = 0; i < Mats.N; i++) if (p.Fill[i] < 0) p.Fill[i] = 0;
            if (!Policies.Valid(p.Policy)) p.Policy = Policies.Threshold;
            return p;
        }
    }

    public sealed class TruckState
    {
        public int Id;
        public string Type = "van";
        public string Status = TruckStatus.Idle;
        public List<int> Stops = new List<int>();     // point indices still to visit
        public List<int> Path = new List<int>();      // node path of the current leg
        public double PathKm, PathPos;                // leg length / progress (km)
        public int Target = -1;                       // point index of the leg, -1 = depot
        public int AtNode;                            // where it stands when not driving
        public double[] Load = new double[Mats.N];   // containers
        public double TimeLeft;                       // minutes of service / unload / breakdown
        public string ResumeStatus = "";              // after a breakdown
        public bool Auto;                             // driven by the Dispatcher
        public bool Voucher;                          // rewarded fuel voucher: this route at half fuel cost
        public double KmToday;
        public int Trips;

        public bool Busy => Status != TruckStatus.Idle;

        public JObj ToJson()
        {
            return new JObj
            {
                ["id"] = (long)Id, ["type"] = Type, ["status"] = Status, ["stops"] = JW.Arr(Stops), ["path"] = JW.Arr(Path), ["path_km"] = PathKm,
                ["pos"] = PathPos, ["target"] = (long)Target, ["at"] = (long)AtNode, ["load"] = JW.Arr(Load), ["left"] = TimeLeft, ["resume"] = ResumeStatus,
                ["auto"] = Auto, ["voucher"] = Voucher, ["km"] = KmToday, ["trips"] = (long)Trips,
            };
        }

        public static TruckState Read(object o)
        {
            var t = new TruckState
            {
                Id = J.I(o, "id"), Type = J.S(o, "type", "van"), Status = J.S(o, "status", TruckStatus.Idle), Stops = JW.Ints(o, "stops"), Path = JW.Ints(o, "path"),
                PathKm = JW.D(o, "path_km"), PathPos = JW.D(o, "pos"), Target = J.I(o, "target", -1), AtNode = J.I(o, "at"), Load = JW.Dbl(o, "load", Mats.N),
                TimeLeft = JW.D(o, "left"), ResumeStatus = J.S(o, "resume"), Auto = J.B(o, "auto"), Voucher = J.B(o, "voucher"), KmToday = JW.D(o, "km"), Trips = J.I(o, "trips"),
            };
            if (!TruckStatus.Valid(t.Status)) t.Status = TruckStatus.Idle;
            return t;
        }
    }

    public sealed class TechJob
    {
        public string Kind = "point";   // point | baler | truck
        public int Target;
        public double DoneAt;           // day minute the fix lands
        public double FreeAt;           // day minute the technician is back
        public JObj ToJson() => new JObj { ["kind"] = Kind, ["target"] = (long)Target, ["done"] = DoneAt, ["free"] = FreeAt };
        public static TechJob Read(object o) => new TechJob { Kind = J.S(o, "kind", "point"), Target = J.I(o, "target"), DoneAt = JW.D(o, "done"), FreeAt = JW.D(o, "free") };
    }

    public sealed class DepotState
    {
        public double[] Intake = new double[Mats.N];      // containers waiting for the count line
        public double[] BalerIn = new double[Mats.N];     // kg sorted, waiting for the baler (pet, alu); glass index = bay kg
        public double[] Press = new double[Mats.N];       // kg in the bale being pressed
        public int[] Bales = new int[Mats.N];             // finished bales in the yard (glass = tonnes in the bay)
        public double Till;                               // counted fees not banked yet (tap the coins)
        public int Lines = 1, YardCap = 12, IntakeCap = 2500;
        public bool BalerDown, Centre, Dispatcher;
        public int Yard => Bales[Mats.Pet] + Bales[Mats.Alu];
        public double IntakeTotal => Intake[0] + Intake[1] + Intake[2];

        public JObj ToJson()
        {
            var b = new List<object> { (long)Bales[0], (long)Bales[1], (long)Bales[2] };
            return new JObj
            {
                ["intake"] = JW.Arr(Intake), ["baler_in"] = JW.Arr(BalerIn), ["press"] = JW.Arr(Press), ["bales"] = b, ["till"] = Till,
                ["lines"] = (long)Lines, ["yard_cap"] = (long)YardCap, ["intake_cap"] = (long)IntakeCap, ["baler_down"] = BalerDown, ["centre"] = Centre, ["dispatcher"] = Dispatcher,
            };
        }

        public static DepotState Read(object o, GameData gd)
        {
            var d = new DepotState
            {
                Intake = JW.Dbl(o, "intake", Mats.N), BalerIn = JW.Dbl(o, "baler_in", Mats.N), Press = JW.Dbl(o, "press", Mats.N), Till = JW.D(o, "till"),
                Lines = J.I(o, "lines", gd.LinesStart), YardCap = J.I(o, "yard_cap", gd.YardCap), IntakeCap = J.I(o, "intake_cap", gd.IntakeCap),
                BalerDown = J.B(o, "baler_down"), Centre = J.B(o, "centre"), Dispatcher = J.B(o, "dispatcher"),
            };
            var b = J.Arr(o, "bales");
            if (b != null) for (int i = 0; i < Mats.N && i < b.Count; i++) d.Bales[i] = Math.Max(0, J.I(b[i]));
            return d;
        }
    }

    public sealed class ActiveEvent
    {
        public string Id;
        public int UntilDay;          // last day it applies (inclusive)
        public int Target = -1;       // point / truck / material index
        public double Mult = 1;
        public double From, To;       // day minutes (match day window)
        public JObj ToJson() => new JObj { ["id"] = Id, ["until"] = (long)UntilDay, ["target"] = (long)Target, ["mult"] = Mult, ["from"] = From, ["to"] = To };
        public static ActiveEvent Read(object o) => new ActiveEvent { Id = J.S(o, "id"), UntilDay = J.I(o, "until"), Target = J.I(o, "target", -1), Mult = JW.D(o, "mult", 1), From = JW.D(o, "from"), To = JW.D(o, "to") };
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
        public int Arrivals, Turned, Deposited, Collected, Counted;
        public double Km, Fuel, Wages, Leases, Power, ShopFees, Fees, Sales, Bonus, Fines, FullMin, BacklogEnd, CountCap;
        public int StarService, StarEfficiency, StarThroughput;
        public double Income => Fees + Sales + Bonus;
        public double Costs => Fuel + Wages + Leases + Power + ShopFees + Fines;
        public double Net => Income - Costs;
        public int Stars => StarService + StarEfficiency + StarThroughput;
        public double TurnedRate => Arrivals <= 0 ? 0 : (double)Turned / Arrivals;
        public double PerKm => Km <= 0.01 ? (Collected > 0 ? 99 : 0) : Collected / Km;

        public JObj ToJson()
        {
            return new JObj
            {
                ["day"] = (long)Day, ["arr"] = (long)Arrivals, ["turned"] = (long)Turned, ["dep"] = (long)Deposited, ["coll"] = (long)Collected, ["counted"] = (long)Counted,
                ["km"] = Km, ["fuel"] = Fuel, ["wages"] = Wages, ["leases"] = Leases, ["power"] = Power, ["shop"] = ShopFees, ["fees"] = Fees, ["sales"] = Sales,
                ["bonus"] = Bonus, ["fines"] = Fines, ["full_min"] = FullMin, ["backlog"] = BacklogEnd, ["count_cap"] = CountCap,
                ["s1"] = (long)StarService, ["s2"] = (long)StarEfficiency, ["s3"] = (long)StarThroughput,
            };
        }

        public static DayStats Read(object o)
        {
            return new DayStats
            {
                Day = J.I(o, "day"), Arrivals = J.I(o, "arr"), Turned = J.I(o, "turned"), Deposited = J.I(o, "dep"), Collected = J.I(o, "coll"), Counted = J.I(o, "counted"),
                Km = JW.D(o, "km"), Fuel = JW.D(o, "fuel"), Wages = JW.D(o, "wages"), Leases = JW.D(o, "leases"), Power = JW.D(o, "power"), ShopFees = JW.D(o, "shop"),
                Fees = JW.D(o, "fees"), Sales = JW.D(o, "sales"), Bonus = JW.D(o, "bonus"), Fines = JW.D(o, "fines"), FullMin = JW.D(o, "full_min"),
                BacklogEnd = JW.D(o, "backlog"), CountCap = JW.D(o, "count_cap"), StarService = J.I(o, "s1"), StarEfficiency = J.I(o, "s2"), StarThroughput = J.I(o, "s3"),
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
        public double Minute = 540;
        public string Phase = Phases.Plan;
        public int Speed = 1;
        public bool Paused;
        public double Cash;
        public double Rating = 50;
        public int Drivers = 1, Techs;
        public List<string> Districts = new List<string>();
        public List<string> Research = new List<string>();
        public string Researching = "";
        public double ResearchLeftS;
        public List<PointState> Points = new List<PointState>();
        public List<TruckState> Trucks = new List<TruckState>();
        public List<TechJob> TechJobs = new List<TechJob>();
        public DepotState Depot = new DepotState();
        public double[] Market = { 1, 1, 1 };
        public List<ActiveEvent> Events = new List<ActiveEvent>();
        public List<PlannedEvent> Planned = new List<PlannedEvent>();
        public DayStats Today = new DayStats { Day = 1 };
        public List<DayStats> History = new List<DayStats>();
        public double NextDispatchAt;
        public bool Tutorial = true;      // guide running: no crises, no penalties
        public bool FuelVoucher;          // rewarded voucher waiting for the next dispatch
        public Dictionary<string, RewardUse> Rewards = new Dictionary<string, RewardUse>();
        public double OfflinePending;     // offline pay shown but not doubled yet (offline_x2)
        public long LastSeen;             // unix seconds of the last save
        public int BestDayStars, ThreeStarDays;
        public long Containers;           // career total collected
        public int Bales;                 // career total pressed
        public List<string> Seen = new List<string>(); // first-time tips shown (sim side markers)

        public JObj ToJson()
        {
            var pts = new List<object>(); foreach (var p in Points) pts.Add(p.ToJson());
            var trs = new List<object>(); foreach (var t in Trucks) trs.Add(t.ToJson());
            var tj = new List<object>(); foreach (var j in TechJobs) tj.Add(j.ToJson());
            var ev = new List<object>(); foreach (var e in Events) ev.Add(e.ToJson());
            var pl = new List<object>(); foreach (var e in Planned) pl.Add(e.ToJson());
            var hi = new List<object>(); foreach (var h in History) hi.Add(h.ToJson());
            var rw = new JObj(); foreach (var kv in Rewards) rw[kv.Key] = kv.Value.ToJson();
            return new JObj
            {
                ["game"] = "bottle-route", ["v"] = (long)Version, ["seed"] = Seed, ["rng"] = RngState, ["day"] = (long)Day, ["minute"] = Minute, ["phase"] = Phase,
                ["speed"] = (long)Speed, ["paused"] = Paused, ["cash"] = Cash, ["rating"] = Rating, ["drivers"] = (long)Drivers, ["techs"] = (long)Techs,
                ["districts"] = JW.Arr(Districts), ["research"] = JW.Arr(Research), ["researching"] = Researching, ["research_left"] = ResearchLeftS,
                ["points"] = pts, ["trucks"] = trs, ["tech_jobs"] = tj, ["depot"] = Depot.ToJson(), ["market"] = JW.Arr(Market), ["events"] = ev, ["planned"] = pl,
                ["today"] = Today.ToJson(), ["history"] = hi, ["next_dispatch"] = NextDispatchAt, ["tutorial"] = Tutorial, ["voucher"] = FuelVoucher,
                ["rewards"] = rw, ["offline_pending"] = OfflinePending, ["last_seen"] = LastSeen, ["best_stars"] = (long)BestDayStars,
                ["three_star_days"] = (long)ThreeStarDays, ["containers"] = Containers, ["bales"] = (long)Bales, ["seen"] = JW.Arr(Seen),
            };
        }
    }
}
