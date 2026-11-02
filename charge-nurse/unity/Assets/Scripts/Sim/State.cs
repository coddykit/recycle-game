// Charge Nurse sim core — engine independent. The whole saved career (State) and its parts. Every class writes and reads
// itself (ToJson / Read); reads are lenient (defaults for missing fields) so older saves load (fleet §14: the codec accepts
// every state the sim can produce — negative cash, a patient mid-corridor, a closed ward, the report screen, a guide step).
using System;
using System.Collections.Generic;

namespace ChargeNurse.Sim
{
    public static class Phases
    {
        public const string Plan = "plan", Live = "live", Report = "report";
        public static bool Valid(string p) => p == Plan || p == Live || p == Report;
    }

    /// <summary>Queue order rule the player sets (plan §4 triage rules). Red always jumps the queue in every rule.</summary>
    public static class QueueRules
    {
        public const string Urgent = "urgent", Balanced = "balanced", Arrival = "arrival";
        public static readonly string[] All = { Urgent, Balanced, Arrival };
        public static bool Valid(string p) => Array.IndexOf(All, p) >= 0;
    }

    /// <summary>Patient pipeline stages: Arrive → Triage → Diagnose → Treat/Admit → Discharge/Clean (plan §4).</summary>
    public static class Stage
    {
        public const string Reg = "reg", Registering = "registering", Triage = "triage", Triaging = "triaging", Test = "test", Testing = "testing",
            Bay = "bay", Treat = "treat", Fast = "fast", Ready = "ready", Board = "board", Move = "move", Surgery = "surgery", Stay = "stay",
            WardReady = "wready", Lounge = "lounge", Discharge = "discharge";
        public static readonly string[] All = { Reg, Registering, Triage, Triaging, Test, Testing, Bay, Treat, Fast, Ready, Board, Move, Surgery, Stay, WardReady, Lounge, Discharge };
        public static bool Valid(string s) => Array.IndexOf(All, s) >= 0;
        /// <summary>Waiting before a doctor has seen the patient (waits count, safe-wait limits apply).</summary>
        public static bool PreDoctor(string s) => s == Reg || s == Registering || s == Triage || s == Triaging || s == Test || s == Testing || s == Bay;
    }

    public static class BedState
    {
        public const string Clean = "clean", Busy = "busy", Dirty = "dirty", Cleaning = "cleaning", Closed = "closed";
        public static bool Valid(string s) => s == Clean || s == Busy || s == Dirty || s == Cleaning || s == Closed;
    }

    static class JW
    {
        public static List<object> Arr(IEnumerable<string> a) { var l = new List<object>(); foreach (var x in a) l.Add(x); return l; }
        public static double Finite(double d) => double.IsNaN(d) || double.IsInfinity(d) ? 0 : d;
        public static double D(object o, string key, double def = 0) => Finite(J.D(o, key, def));
    }

    public sealed class Patient
    {
        public int Id;
        public int Sev;                 // true severity (0 green … 3 red); can rise once by waiting too long
        public int Tag = -1;            // the colour on the card, -1 = not triaged yet
        public string Test = "";        // xray | lab | ct | "" (done or none)
        public bool Admit, NeedsOr, Doc, Seen, Worsened, Guided, Amb, NoteLost, Fast;
        public string Ward = "";        // obs | general | surgical | icu (target ward when admitted)
        public string Stage = Sim.Stage.Reg;
        public int Bed = -1, Room = -1; // where the patient is (bay / ward bed, test room / OR)
        public int Dest = -1;           // bed (or -2 - room) reserved for a porter move
        public double Left;             // minutes left of the timed step (triage, test, treatment, stay, paperwork, move)
        public double Wait;             // minutes waited before a doctor (door-to-doctor)
        public double Total;            // minutes in the hospital
        public int Nights;

        public JObj ToJson()
        {
            return new JObj
            {
                ["id"] = (long)Id, ["sev"] = (long)Sev, ["tag"] = (long)Tag, ["test"] = Test, ["admit"] = Admit, ["or"] = NeedsOr, ["doc"] = Doc, ["seen"] = Seen,
                ["worse"] = Worsened, ["guided"] = Guided, ["amb"] = Amb, ["note"] = NoteLost, ["fast"] = Fast, ["ward"] = Ward, ["stage"] = Stage,
                ["bed"] = (long)Bed, ["room"] = (long)Room, ["dest"] = (long)Dest, ["left"] = Left, ["wait"] = Wait, ["total"] = Total, ["nights"] = (long)Nights,
            };
        }

        public static Patient Read(object o)
        {
            return new Patient
            {
                Id = J.I(o, "id"), Sev = J.I(o, "sev"), Tag = J.I(o, "tag", -1), Test = J.S(o, "test"), Admit = J.B(o, "admit"), NeedsOr = J.B(o, "or"),
                Doc = J.B(o, "doc"), Seen = J.B(o, "seen"), Worsened = J.B(o, "worse"), Guided = J.B(o, "guided"), Amb = J.B(o, "amb"), NoteLost = J.B(o, "note"), Fast = J.B(o, "fast"),
                Ward = J.S(o, "ward"), Stage = J.S(o, "stage", Sim.Stage.Reg), Bed = J.I(o, "bed", -1), Room = J.I(o, "room", -1), Dest = J.I(o, "dest", -1),
                Left = JW.D(o, "left"), Wait = JW.D(o, "wait"), Total = JW.D(o, "total"), Nights = J.I(o, "nights"),
            };
        }
    }

    public sealed class Bed
    {
        public int Zone;                // map zone (a bays zone or a ward)
        public int Slot;
        public string State = BedState.Clean;
        public int Patient = -1;        // patient id
        public double Left;             // cleaning minutes left
        public bool Guided;             // the guide's bay: waits for the player's tap
        public bool Bay;                // ED treatment bay (else a ward bed)

        public JObj ToJson() => new JObj
        {
            ["zone"] = (long)Zone, ["slot"] = (long)Slot, ["state"] = State, ["patient"] = (long)Patient, ["left"] = Left, ["guided"] = Guided, ["bay"] = Bay,
        };

        public static Bed Read(object o) => new Bed
        {
            Zone = J.I(o, "zone", -1), Slot = J.I(o, "slot"), State = J.S(o, "state", BedState.Clean), Patient = J.I(o, "patient", -1), Left = JW.D(o, "left"),
            Guided = J.B(o, "guided"), Bay = J.B(o, "bay"),
        };
    }

    public sealed class Room
    {
        public string Kind = "xray";    // xray | lab | ct | or
        public int Zone;
        public int Techs;               // technicians of this room (the OR needs a doctor instead)
        public double Down;             // minutes left of a breakdown / power cut
        public int Patient = -1;
        public JObj ToJson() => new JObj { ["kind"] = Kind, ["zone"] = (long)Zone, ["techs"] = (long)Techs, ["down"] = Down, ["patient"] = (long)Patient };
        public static Room Read(object o) => new Room { Kind = J.S(o, "kind", "xray"), Zone = J.I(o, "zone", -1), Techs = J.I(o, "techs"), Down = JW.D(o, "down"), Patient = J.I(o, "patient", -1) };
    }

    public sealed class AmbulanceCall
    {
        public int Sev = Sim.Sev.Orange;
        public double Left;             // minutes to answer before the default (accept, or the diversion rule) applies
        public bool Guided;
        public JObj ToJson() => new JObj { ["sev"] = (long)Sev, ["left"] = Left, ["guided"] = Guided };
        public static AmbulanceCall Read(object o) => new AmbulanceCall { Sev = J.I(o, "sev", Sim.Sev.Orange), Left = JW.D(o, "left"), Guided = J.B(o, "guided") };
    }

    public sealed class ActiveEvent
    {
        public string Id;
        public int UntilDay;            // last day it applies (inclusive)
        public string Target = "";      // team / room / ward it hit
        public double Mult = 1;
        public double Left;             // minutes left (power cut, scanner down)
        public JObj ToJson() => new JObj { ["id"] = Id, ["until"] = (long)UntilDay, ["target"] = Target, ["mult"] = Mult, ["left"] = Left };
        public static ActiveEvent Read(object o) => new ActiveEvent { Id = J.S(o, "id"), UntilDay = J.I(o, "until"), Target = J.S(o, "target"), Mult = JW.D(o, "mult", 1), Left = JW.D(o, "left") };
    }

    public sealed class PlannedEvent
    {
        public string Id;
        public double At;
        public JObj ToJson() => new JObj { ["id"] = Id, ["at"] = At };
        public static PlannedEvent Read(object o) => new PlannedEvent { Id = J.S(o, "id"), At = JW.D(o, "at") };
    }

    public sealed class DayStats
    {
        public int Day;
        public int Arrivals, Treated, Admitted, Lwbs, Worsened, Transferred, Diverted, Ambulances, NightTreated;
        public double WaitSum, WaitN, BedMin, BlockedMin;
        public double Fees, Ward, Bonus, Wages, Supplies, Upkeep, Fines, NightFees;
        public int StarCare, StarWaits, StarFlow;
        public bool Rushed;
        public double Income => Fees + Ward + Bonus;
        public double Costs => Wages + Supplies + Upkeep + Fines;
        public double Net => Income - Costs;
        public int Stars => StarCare + StarWaits + StarFlow;
        public int Bad => Lwbs + Worsened + Transferred;
        public int Cases => Treated + Lwbs + Transferred;
        public double BadRate => Cases <= 0 ? 0 : (double)Bad / Math.Max(1, Cases);
        public double AvgWait => WaitN <= 0 ? 0 : WaitSum / WaitN;
        public double BlockedShare => BedMin <= 0 ? 0 : BlockedMin / BedMin;

        public JObj ToJson() => new JObj
        {
            ["day"] = (long)Day, ["arr"] = (long)Arrivals, ["treated"] = (long)Treated, ["admitted"] = (long)Admitted, ["lwbs"] = (long)Lwbs,
            ["worse"] = (long)Worsened, ["transfer"] = (long)Transferred, ["divert"] = (long)Diverted, ["amb"] = (long)Ambulances, ["night_n"] = (long)NightTreated,
            ["wait_sum"] = WaitSum, ["wait_n"] = WaitN, ["bed_min"] = BedMin, ["blocked"] = BlockedMin, ["fees"] = Fees, ["ward"] = Ward, ["bonus"] = Bonus,
            ["wages"] = Wages, ["supplies"] = Supplies, ["upkeep"] = Upkeep, ["fines"] = Fines, ["night_fees"] = NightFees,
            ["s1"] = (long)StarCare, ["s2"] = (long)StarWaits, ["s3"] = (long)StarFlow, ["rushed"] = Rushed,
        };

        public static DayStats Read(object o) => new DayStats
        {
            Day = J.I(o, "day"), Arrivals = J.I(o, "arr"), Treated = J.I(o, "treated"), Admitted = J.I(o, "admitted"), Lwbs = J.I(o, "lwbs"),
            Worsened = J.I(o, "worse"), Transferred = J.I(o, "transfer"), Diverted = J.I(o, "divert"), Ambulances = J.I(o, "amb"), NightTreated = J.I(o, "night_n"),
            WaitSum = JW.D(o, "wait_sum"), WaitN = JW.D(o, "wait_n"), BedMin = JW.D(o, "bed_min"), BlockedMin = JW.D(o, "blocked"), Fees = JW.D(o, "fees"),
            Ward = JW.D(o, "ward"), Bonus = JW.D(o, "bonus"), Wages = JW.D(o, "wages"), Supplies = JW.D(o, "supplies"), Upkeep = JW.D(o, "upkeep"),
            Fines = JW.D(o, "fines"), NightFees = JW.D(o, "night_fees"), StarCare = J.I(o, "s1"), StarWaits = J.I(o, "s2"), StarFlow = J.I(o, "s3"), Rushed = J.B(o, "rushed"),
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
        public double Minute = 420;
        public string Phase = Phases.Plan;
        public int Speed = 1;
        public bool Paused;
        public double Cash;
        public double Reputation = 50;
        public double Burnout;
        public Dictionary<string, int> Teams = new Dictionary<string, int>();
        public Dictionary<string, double> Fatigue = new Dictionary<string, double>();
        public Dictionary<string, double> BusyMin = new Dictionary<string, double>();   // today: busy member-minutes per team
        public Dictionary<string, double> CapMin = new Dictionary<string, double>();    // today: available member-minutes per team
        public List<string> Wings = new List<string>();
        public List<string> Research = new List<string>();
        public string Researching = "";
        public double ResearchLeft;       // live game minutes
        public List<Bed> Beds = new List<Bed>();
        public List<Room> Rooms = new List<Room>();
        public List<Patient> Patients = new List<Patient>();
        public int NextPatient = 1;
        public AmbulanceCall Ambulance;
        public List<ActiveEvent> Events = new List<ActiveEvent>();
        public List<PlannedEvent> Planned = new List<PlannedEvent>();
        public DayStats Today = new DayStats { Day = 1 };
        public List<DayStats> History = new List<DayStats>();
        public string Queue = QueueRules.Balanced;
        public int DivertAt;              // diversion rule: ED full share in % (0 = never divert by itself)
        public bool HandoverDone;
        public bool Surge;                // today's surge team (+1 nurse, +1 doctor), plan §4 "surge plans"
        public string SickTeam = "";
        public string FloatTeam = "";
        public double FloatLeft;          // live game minutes of the rewarded float staff
        public bool NeighborUsed;         // rewarded "call the neighbour hospital": once per crisis
        public bool Tutorial = true;      // guide running: no crises, nobody worsens, nobody leaves
        public bool ArrivalsOn = true;    // the guide holds walk-ins until its ambulance step
        public Dictionary<string, RewardUse> Rewards = new Dictionary<string, RewardUse>();
        public double OfflinePending;
        public long LastSeen;
        public int BestStars, GoodDays;
        public long CareerTreated;
        public List<string> Seen = new List<string>();

        public JObj ToJson()
        {
            static JObj Dict<T>(Dictionary<string, T> d) { var o = new JObj(); foreach (var kv in d) o[kv.Key] = kv.Value is int i ? (object)(long)i : kv.Value; return o; }
            var beds = new List<object>(); foreach (var b in Beds) beds.Add(b.ToJson());
            var rooms = new List<object>(); foreach (var r in Rooms) rooms.Add(r.ToJson());
            var pts = new List<object>(); foreach (var p in Patients) pts.Add(p.ToJson());
            var ev = new List<object>(); foreach (var e in Events) ev.Add(e.ToJson());
            var pl = new List<object>(); foreach (var e in Planned) pl.Add(e.ToJson());
            var hi = new List<object>(); foreach (var h in History) hi.Add(h.ToJson());
            var rw = new JObj(); foreach (var kv in Rewards) rw[kv.Key] = kv.Value.ToJson();
            return new JObj
            {
                ["game"] = "charge-nurse", ["v"] = (long)Version, ["seed"] = Seed, ["rng"] = RngState, ["day"] = (long)Day, ["minute"] = Minute, ["phase"] = Phase,
                ["speed"] = (long)Speed, ["paused"] = Paused, ["cash"] = Cash, ["reputation"] = Reputation, ["burnout"] = Burnout,
                ["teams"] = Dict(Teams), ["fatigue"] = Dict(Fatigue), ["busy_min"] = Dict(BusyMin), ["cap_min"] = Dict(CapMin),
                ["wings"] = JW.Arr(Wings), ["research"] = JW.Arr(Research), ["researching"] = Researching, ["research_left"] = ResearchLeft,
                ["beds"] = beds, ["rooms"] = rooms, ["patients"] = pts, ["next_patient"] = (long)NextPatient,
                ["ambulance"] = Ambulance != null ? Ambulance.ToJson() : null, ["events"] = ev, ["planned"] = pl, ["today"] = Today.ToJson(), ["history"] = hi,
                ["queue"] = Queue, ["divert_at"] = (long)DivertAt, ["handover"] = HandoverDone, ["surge"] = Surge, ["sick"] = SickTeam, ["float_team"] = FloatTeam, ["float_left"] = FloatLeft,
                ["neighbor"] = NeighborUsed, ["tutorial"] = Tutorial, ["arrivals_on"] = ArrivalsOn, ["rewards"] = rw, ["offline_pending"] = OfflinePending,
                ["last_seen"] = LastSeen, ["best_stars"] = (long)BestStars, ["good_days"] = (long)GoodDays, ["career_treated"] = CareerTreated, ["seen"] = JW.Arr(Seen),
            };
        }
    }
}
