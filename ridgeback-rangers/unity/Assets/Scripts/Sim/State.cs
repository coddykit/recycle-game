// Ridgeback Rangers sim core — engine independent. The whole saved career (State) and its parts. Every class writes and reads
// itself (ToJson / Read); reads are lenient (defaults for missing fields) so older saves load (fleet §14: the codec accepts
// every state the sim can produce — negative cash, a ranger mid-road, an open escape alarm, the tram mid-loop, the report
// screen, a guide step, an egg half warmed).
using System;
using System.Collections.Generic;

namespace RidgebackRangers.Sim
{
    public static class Phases
    {
        public const string Plan = "plan", Live = "live", Report = "report";
        public static bool Valid(string p) => p == Plan || p == Live || p == Report;
    }

    public static class RangerStatus
    {
        public const string Idle = "idle", Travel = "travel", Work = "work", Back = "back", Dig = "dig";
        public static bool Valid(string s) => s == Idle || s == Travel || s == Work || s == Back || s == Dig;
    }

    public static class Health
    {
        public const string Ok = "ok", Green = "green", Yellow = "yellow", Red = "red", Off = "off";
        public static readonly string[] Sick = { Green, Yellow, Red };
        public static bool Valid(string h) => h == Ok || h == Green || h == Yellow || h == Red || h == Off;
    }

    public static class TramPolicy
    {
        public const string Full = "full", Interval = "interval";
        public static bool Valid(string p) => p == Full || p == Interval;
        public static readonly int[] Intervals = { 10, 20, 30 };
    }

    static class JW
    {
        public static List<object> Arr(IEnumerable<int> a) { var l = new List<object>(); foreach (var x in a) l.Add((long)x); return l; }
        public static List<object> Arr(IEnumerable<string> a) { var l = new List<object>(); foreach (var x in a) l.Add(x); return l; }
        public static List<int> Ints(object o, string key) { var r = new List<int>(); foreach (var x in J.Arr(o, key) ?? new List<object>()) r.Add(J.I(x)); return r; }
        public static double Finite(double d) => double.IsNaN(d) || double.IsInfinity(d) ? 0 : d;
        public static double D(object o, string key, double def = 0) => Finite(J.D(o, key, def));
        public static double Clamp01(double d) => Math.Max(0, Math.Min(1, Finite(d)));
    }

    public sealed class PaddockState
    {
        public int Node;
        public int Fence = 1;              // 1 wood, 2 steel, 3 double moat
        public double Integrity = 1;       // fence tiles 0..1
        public bool Water, Shade;
        public int Expand;                 // expansions bought
        public JObj ToJson() => new JObj { ["node"] = (long)Node, ["fence"] = (long)Fence, ["integrity"] = Integrity, ["water"] = Water, ["shade"] = Shade, ["expand"] = (long)Expand };
        public static PaddockState Read(object o) => new PaddockState
        {
            Node = J.I(o, "node", -1), Fence = Math.Max(1, Math.Min(3, J.I(o, "fence", 1))), Integrity = JW.Clamp01(J.D(o, "integrity", 1)),
            Water = J.B(o, "water"), Shade = J.B(o, "shade"), Expand = Math.Max(0, J.I(o, "expand")),
        };
    }

    public sealed class AnimalState
    {
        public int Id;
        public string Species = "";
        public int Paddock = -1;           // -1 = nursery (not on exhibit)
        public bool Young;
        public int AgeDays;
        public double Hunger, Mood = 0.6;
        public string Health = Sim.Health.Ok;
        public double ShowUntil;           // day minute: eating (feeding show)
        public JObj ToJson() => new JObj
        {
            ["id"] = (long)Id, ["sp"] = Species, ["paddock"] = (long)Paddock, ["young"] = Young, ["age"] = (long)AgeDays, ["hunger"] = Hunger, ["mood"] = Mood,
            ["health"] = Health, ["show"] = ShowUntil,
        };
        public static AnimalState Read(object o)
        {
            var a = new AnimalState
            {
                Id = J.I(o, "id"), Species = J.S(o, "sp"), Paddock = J.I(o, "paddock", -1), Young = J.B(o, "young"), AgeDays = Math.Max(0, J.I(o, "age")),
                Hunger = JW.Clamp01(J.D(o, "hunger")), Mood = JW.Clamp01(J.D(o, "mood", 0.6)), Health = J.S(o, "health", Sim.Health.Ok), ShowUntil = JW.D(o, "show"),
            };
            if (!Sim.Health.Valid(a.Health)) a.Health = Sim.Health.Ok;
            return a;
        }
    }

    public sealed class StationState
    {
        public double Crates;
        public int Plan = 8;               // morning delivery (feed board)
        public bool Cooler;
        public JObj ToJson() => new JObj { ["crates"] = Crates, ["plan"] = (long)Plan, ["cooler"] = Cooler };
        public static StationState Read(object o) => new StationState { Crates = Math.Max(0, JW.D(o, "crates")), Plan = Math.Max(0, J.I(o, "plan", 8)), Cooler = J.B(o, "cooler") };
    }

    public sealed class RangerState
    {
        public int Id;
        public string Role = Roles.Feeder;
        public string Status = RangerStatus.Idle;
        public List<int> Path = new List<int>();
        public double PathKm, PathPos;
        public int AtNode;
        public int Pin = -1;               // pin id it works on
        public double TimeLeft;            // work / dig minutes
        public int Leg;                    // feeder: 1 = to the barn, 2 = to the pin
        public bool Temp;                  // borrowed ranger (rewarded): leaves after the job
        public int Dig = -1;               // dig site index while digging
        public bool Busy => Status != RangerStatus.Idle && Status != RangerStatus.Back;
        public JObj ToJson() => new JObj
        {
            ["id"] = (long)Id, ["role"] = Role, ["status"] = Status, ["path"] = JW.Arr(Path), ["path_km"] = PathKm, ["pos"] = PathPos, ["at"] = (long)AtNode,
            ["pin"] = (long)Pin, ["left"] = TimeLeft, ["leg"] = (long)Leg, ["temp"] = Temp, ["dig"] = (long)Dig,
        };
        public static RangerState Read(object o)
        {
            var r = new RangerState
            {
                Id = J.I(o, "id"), Role = J.S(o, "role", Roles.Feeder), Status = J.S(o, "status", RangerStatus.Idle), Path = JW.Ints(o, "path"),
                PathKm = Math.Max(0, JW.D(o, "path_km")), PathPos = Math.Max(0, JW.D(o, "pos")), AtNode = J.I(o, "at"), Pin = J.I(o, "pin", -1),
                TimeLeft = JW.D(o, "left"), Leg = J.I(o, "leg"), Temp = J.B(o, "temp"), Dig = J.I(o, "dig", -1),
            };
            if (!RangerStatus.Valid(r.Status)) r.Status = RangerStatus.Idle;
            return r;
        }
    }

    public sealed class PinState
    {
        public int Id;
        public string Kind = PinKinds.Hungry;
        public int Node;                   // where the ranger goes
        public int Paddock = -1, Animal = -1;
        public string Zone = "";
        public string Severity = "";       // sick: green | yellow | red
        public double Created, Deadline;   // day minutes
        public int Ranger = -1;            // ranger id on the way / working
        public bool Call;                  // daily Ranger Call pin (minor)
        public JObj ToJson() => new JObj
        {
            ["id"] = (long)Id, ["kind"] = Kind, ["node"] = (long)Node, ["paddock"] = (long)Paddock, ["animal"] = (long)Animal, ["zone"] = Zone, ["sev"] = Severity,
            ["created"] = Created, ["deadline"] = Deadline, ["ranger"] = (long)Ranger, ["call"] = Call,
        };
        public static PinState Read(object o) => new PinState
        {
            Id = J.I(o, "id"), Kind = J.S(o, "kind"), Node = J.I(o, "node", -1), Paddock = J.I(o, "paddock", -1), Animal = J.I(o, "animal", -1), Zone = J.S(o, "zone"),
            Severity = J.S(o, "sev"), Created = JW.D(o, "created"), Deadline = JW.D(o, "deadline"), Ranger = J.I(o, "ranger", -1), Call = J.B(o, "call"),
        };
    }

    public sealed class TramState
    {
        public int Cars = 1;
        public bool Running;               // false = boarding at the station
        public int Riders;
        public double Joy;                 // joy collected by the riders on board (sum)
        public double Wait;                // minutes the riders on board waited in the queue (sum)
        public double Pos;                 // km along the loop
        public int NextStop = 1;           // index in the stop list the tram drives to
        public double Dwell;               // minutes left at a stop
        public double BoardSince;          // day minute the boarding started
        public string Policy = TramPolicy.Full;
        public int Interval = 20;
        public JObj ToJson() => new JObj
        {
            ["cars"] = (long)Cars, ["run"] = Running, ["riders"] = (long)Riders, ["joy"] = Joy, ["wait"] = Wait, ["pos"] = Pos, ["next"] = (long)NextStop,
            ["dwell"] = Dwell, ["since"] = BoardSince, ["policy"] = Policy, ["interval"] = (long)Interval,
        };
        public static TramState Read(object o)
        {
            var t = new TramState
            {
                Cars = Math.Max(1, J.I(o, "cars", 1)), Running = J.B(o, "run"), Riders = Math.Max(0, J.I(o, "riders")), Joy = Math.Max(0, JW.D(o, "joy")),
                Wait = Math.Max(0, JW.D(o, "wait")), Pos = Math.Max(0, JW.D(o, "pos")), NextStop = Math.Max(1, J.I(o, "next", 1)), Dwell = Math.Max(0, JW.D(o, "dwell")),
                BoardSince = JW.D(o, "since"), Policy = J.S(o, "policy", TramPolicy.Full), Interval = J.I(o, "interval", 20),
            };
            if (!TramPolicy.Valid(t.Policy)) t.Policy = TramPolicy.Full;
            if (Array.IndexOf(TramPolicy.Intervals, t.Interval) < 0) t.Interval = 20;
            return t;
        }
    }

    public sealed class VisitorGroup
    {
        public int N;
        public double At;
        public JObj ToJson() => new JObj { ["n"] = (long)N, ["at"] = At };
        public static VisitorGroup Read(object o) => new VisitorGroup { N = Math.Max(0, J.I(o, "n")), At = JW.D(o, "at") };
    }

    public sealed class EggState
    {
        public string Species = "";
        public double LeftS;               // real seconds to hatch
        public double TotalS;
        public bool Checked;               // temperature in the band (else the timer runs at half speed)
        public bool SecondCheckDone;       // the mid-way temperature drift was handled
        public JObj ToJson() => new JObj { ["sp"] = Species, ["left"] = LeftS, ["total"] = TotalS, ["checked"] = Checked, ["second"] = SecondCheckDone };
        public static EggState Read(object o) => new EggState
        {
            Species = J.S(o, "sp"), LeftS = Math.Max(0, JW.D(o, "left")), TotalS = Math.Max(1, JW.D(o, "total", 1)), Checked = J.B(o, "checked"), SecondCheckDone = J.B(o, "second"),
        };
    }

    public sealed class ActiveEvent
    {
        public string Id;
        public int UntilDay;
        public double Mult = 1;
        public string Zone = "";
        public double Until;               // day minute (rain squall end), 0 = all day
        public JObj ToJson() => new JObj { ["id"] = Id, ["until"] = (long)UntilDay, ["mult"] = Mult, ["zone"] = Zone, ["until_min"] = Until };
        public static ActiveEvent Read(object o) => new ActiveEvent { Id = J.S(o, "id"), UntilDay = J.I(o, "until"), Mult = JW.D(o, "mult", 1), Zone = J.S(o, "zone"), Until = JW.D(o, "until_min") };
    }

    public sealed class PlannedEvent
    {
        public string Id;
        public double At;
        public bool Call;                  // a Ranger Call pin rather than an island event
        public JObj ToJson() => new JObj { ["id"] = Id, ["at"] = At, ["call"] = Call };
        public static PlannedEvent Read(object o) => new PlannedEvent { Id = J.S(o, "id"), At = JW.D(o, "at"), Call = J.B(o, "call") };
    }

    public sealed class DayStats
    {
        public int Day;
        public int Visitors, Rides, Walked, PinsOpened, PinsResolved, PinsFailed;
        public double Tickets, Snacks, Gifts, Donations, CpEarned, Wages, Feed, Upkeep, Fines, Joy, SatSum, CareSum;
        public int SatN, CareN;
        public int StarCare, StarVisitors, StarCalm;
        public List<string> Failed = new List<string>();
        public double Income => Tickets + Snacks + Gifts + Donations;
        public double Costs => Wages + Feed + Upkeep + Fines;
        public double Net => Income - Costs;
        public int Stars => StarCare + StarVisitors + StarCalm;
        public double Satisfaction => SatN <= 0 ? 0 : SatSum / SatN;
        public double Care => CareN <= 0 ? 0 : CareSum / CareN;
        public double Calm => PinsResolved + PinsFailed <= 0 ? 1 : (double)PinsResolved / (PinsResolved + PinsFailed);

        public JObj ToJson() => new JObj
        {
            ["day"] = (long)Day, ["visitors"] = (long)Visitors, ["rides"] = (long)Rides, ["walked"] = (long)Walked, ["pins"] = (long)PinsOpened,
            ["resolved"] = (long)PinsResolved, ["failed_n"] = (long)PinsFailed, ["tickets"] = Tickets, ["snacks"] = Snacks, ["gifts"] = Gifts,
            ["donations"] = Donations, ["cp"] = CpEarned, ["wages"] = Wages, ["feed"] = Feed, ["upkeep"] = Upkeep, ["fines"] = Fines, ["joy"] = Joy,
            ["sat"] = SatSum, ["sat_n"] = (long)SatN, ["care"] = CareSum, ["care_n"] = (long)CareN, ["s1"] = (long)StarCare, ["s2"] = (long)StarVisitors,
            ["s3"] = (long)StarCalm, ["failed"] = JW.Arr(Failed),
        };

        public static DayStats Read(object o) => new DayStats
        {
            Day = J.I(o, "day"), Visitors = J.I(o, "visitors"), Rides = J.I(o, "rides"), Walked = J.I(o, "walked"), PinsOpened = J.I(o, "pins"),
            PinsResolved = J.I(o, "resolved"), PinsFailed = J.I(o, "failed_n"), Tickets = JW.D(o, "tickets"), Snacks = JW.D(o, "snacks"), Gifts = JW.D(o, "gifts"),
            Donations = JW.D(o, "donations"), CpEarned = JW.D(o, "cp"), Wages = JW.D(o, "wages"), Feed = JW.D(o, "feed"), Upkeep = JW.D(o, "upkeep"),
            Fines = JW.D(o, "fines"), Joy = JW.D(o, "joy"), SatSum = JW.D(o, "sat"), SatN = J.I(o, "sat_n"), CareSum = JW.D(o, "care"), CareN = J.I(o, "care_n"),
            StarCare = J.I(o, "s1"), StarVisitors = J.I(o, "s2"), StarCalm = J.I(o, "s3"), Failed = J.StrList(o, "failed") ?? new List<string>(),
        };
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
        public double Minute = 480;
        public string Phase = Phases.Plan;
        public int Speed = 1;
        public bool Paused;
        public double Cash, Cp, Rating = 50;
        public int NextId = 1;
        public List<string> Zones = new List<string>();
        public List<PaddockState> Paddocks = new List<PaddockState>();
        public List<AnimalState> Animals = new List<AnimalState>();
        public Dictionary<string, StationState> Stations = new Dictionary<string, StationState>();
        public List<RangerState> Rangers = new List<RangerState>();
        public List<PinState> Pins = new List<PinState>();
        public TramState Tram = new TramState();
        public List<VisitorGroup> Queue = new List<VisitorGroup>();
        public bool GateOpen;
        public List<EggState> Eggs = new List<EggState>();      // incubators in use (index = slot)
        public List<string> EggsWaiting = new List<string>();   // dug eggs with no free incubator yet
        public int Incubators = 2;
        public Dictionary<string, double> Shards = new Dictionary<string, double>();   // dig site id -> % toward its next egg
        public Dictionary<string, int> DigEggs = new Dictionary<string, int>();        // dig site id -> eggs found there
        public List<string> Found = new List<string>();          // species ever in the sanctuary (the species log)
        public List<string> Scouted = new List<string>();        // dig sites whose hidden species is revealed
        public List<string> Upgrades = new List<string>();       // lane, vet_beds, gift, hatch_tech, chief, feed_run2
        public int Cafe;
        public bool ChiefOn;
        public List<ActiveEvent> Events = new List<ActiveEvent>();
        public List<PlannedEvent> Planned = new List<PlannedEvent>();
        public DayStats Today = new DayStats { Day = 1 };
        public List<DayStats> History = new List<DayStats>();
        public bool Tutorial = true;      // guide running: no crises, no expiry, no penalties
        public Dictionary<string, RewardUse> Rewards = new Dictionary<string, RewardUse>();
        public double OfflinePending;
        public long LastSeen;
        public int BestDayStars, ThreeStarDays;
        public long VisitorsTotal;
        public int Hatched;
        public List<string> Seen = new List<string>();

        public JObj ToJson()
        {
            var pd = new List<object>(); foreach (var p in Paddocks) pd.Add(p.ToJson());
            var an = new List<object>(); foreach (var a in Animals) an.Add(a.ToJson());
            var stn = new JObj(); foreach (var kv in Stations) stn[kv.Key] = kv.Value.ToJson();
            var rg = new List<object>(); foreach (var r in Rangers) rg.Add(r.ToJson());
            var pi = new List<object>(); foreach (var p in Pins) pi.Add(p.ToJson());
            var qu = new List<object>(); foreach (var g in Queue) qu.Add(g.ToJson());
            var eg = new List<object>(); foreach (var e in Eggs) eg.Add(e.ToJson());
            var sh = new JObj(); foreach (var kv in Shards) sh[kv.Key] = kv.Value;
            var de = new JObj(); foreach (var kv in DigEggs) de[kv.Key] = (long)kv.Value;
            var ev = new List<object>(); foreach (var e in Events) ev.Add(e.ToJson());
            var pl = new List<object>(); foreach (var e in Planned) pl.Add(e.ToJson());
            var hi = new List<object>(); foreach (var h in History) hi.Add(h.ToJson());
            var rw = new JObj(); foreach (var kv in Rewards) rw[kv.Key] = kv.Value.ToJson();
            return new JObj
            {
                ["game"] = "ridgeback-rangers", ["v"] = (long)Version, ["seed"] = Seed, ["rng"] = RngState, ["day"] = (long)Day, ["minute"] = Minute, ["phase"] = Phase,
                ["speed"] = (long)Speed, ["paused"] = Paused, ["cash"] = Cash, ["cp"] = Cp, ["rating"] = Rating, ["next_id"] = (long)NextId,
                ["zones"] = JW.Arr(Zones), ["paddocks"] = pd, ["animals"] = an, ["stations"] = stn, ["rangers"] = rg, ["pins"] = pi, ["tram"] = Tram.ToJson(),
                ["queue"] = qu, ["gate"] = GateOpen, ["eggs"] = eg, ["eggs_waiting"] = JW.Arr(EggsWaiting), ["incubators"] = (long)Incubators, ["shards"] = sh, ["dig_eggs"] = de, ["found"] = JW.Arr(Found),
                ["scouted"] = JW.Arr(Scouted), ["upgrades"] = JW.Arr(Upgrades), ["cafe"] = (long)Cafe, ["chief_on"] = ChiefOn, ["events"] = ev, ["planned"] = pl,
                ["today"] = Today.ToJson(), ["history"] = hi, ["tutorial"] = Tutorial, ["rewards"] = rw, ["offline_pending"] = OfflinePending,
                ["last_seen"] = LastSeen, ["best_stars"] = (long)BestDayStars, ["three_star_days"] = (long)ThreeStarDays, ["visitors_total"] = VisitorsTotal,
                ["hatched"] = (long)Hatched, ["seen"] = JW.Arr(Seen),
            };
        }
    }
}
