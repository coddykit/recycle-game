// Docket Day sim core — engine independent. The whole saved career (State) and its parts. Every class writes and reads
// itself (ToJson / Read); reads are lenient (defaults for missing fields) so older saves load (fleet §14: the codec accepts
// every state the sim can produce — negative cash, a hearing in session, a double-booked lawyer, the report screen, an
// unbilled day, a sick paralegal, the guide half way).
using System;
using System.Collections.Generic;

namespace DocketDay.Sim
{
    public static class Phases
    {
        public const string Plan = "plan", Live = "live", Report = "report";
        public static bool Valid(string p) => p == Plan || p == Live || p == Report;
    }

    public static class MatterStatus
    {
        /// <summary>upcoming = generated, not arrived yet (case preview); intake = waiting at reception; open = accepted;
        /// hearing = in session.</summary>
        public const string Upcoming = "upcoming", Intake = "intake", Open = "open", Hearing = "hearing";
        public static bool Valid(string s) => s == Upcoming || s == Intake || s == Open || s == Hearing;
    }

    /// <summary>Special attendee ids on a hearing task.</summary>
    public static class Attend
    {
        public const int None = -1, Visiting = -2;
    }

    static class JW
    {
        public static List<object> Arr(IEnumerable<int> a) { var l = new List<object>(); foreach (var x in a) l.Add((long)x); return l; }
        public static List<object> Arr(IEnumerable<string> a) { var l = new List<object>(); foreach (var x in a) l.Add(x); return l; }
        public static List<object> Arr(IEnumerable<double> a) { var l = new List<object>(); foreach (var x in a) l.Add(x); return l; }
        public static List<int> Ints(object o, string key) { var r = new List<int>(); foreach (var x in J.Arr(o, key) ?? new List<object>()) r.Add(J.I(x)); return r; }
        public static List<double> Dbls(object o, string key) { var r = new List<double>(); foreach (var x in J.Arr(o, key) ?? new List<object>()) r.Add(Finite(J.D(x))); return r; }
        public static double Finite(double d) => double.IsNaN(d) || double.IsInfinity(d) ? 0 : d;
        public static double D(object o, string key, double def = 0) => Finite(J.D(o, key, def));
    }

    public sealed class StaffState
    {
        public int Id;
        public string Role = "intern";
        public int Name;                 // index into the name pool (strings staff.name.<n>)
        public double HoursLeft;         // today
        public double HoursWorked;       // today
        public double Fatigue;           // 0..100
        public bool Sick;                // out today (crisis)
        public int TempUntil;            // > 0: a day-pass hire that leaves after that day
        public int Outfit;               // cosmetic tint index

        public JObj ToJson() => new JObj
        {
            ["id"] = (long)Id, ["role"] = Role, ["name"] = (long)Name, ["left"] = HoursLeft, ["worked"] = HoursWorked, ["fatigue"] = Fatigue,
            ["sick"] = Sick, ["temp"] = (long)TempUntil, ["outfit"] = (long)Outfit,
        };

        public static StaffState Read(object o) => new StaffState
        {
            Id = J.I(o, "id"), Role = J.S(o, "role", "intern"), Name = J.I(o, "name"), HoursLeft = Math.Max(0, JW.D(o, "left")), HoursWorked = Math.Max(0, JW.D(o, "worked")),
            Fatigue = Math.Max(0, Math.Min(100, JW.D(o, "fatigue"))), Sick = J.B(o, "sick"), TempUntil = J.I(o, "temp"), Outfit = J.I(o, "outfit"),
        };
    }

    public sealed class TaskState
    {
        public string Kind = Kinds.File;
        public double Need, Done;        // work units (hours × skill); a hearing: Need = hearing hours
        public int Due;                  // day (hearing: court date; others: soft deadline)
        public int Slot;                 // hearing slot index (Gd.HearingSlots)
        public int Staff = Attend.None;  // assignee / attendee
        public bool Late;                // soft deadline missed (sanctioned once; still to do)
        public bool Finished;

        public double Left => Math.Max(0, Need - Done);
        public double Frac => Need <= 0 ? (Finished ? 1 : 0) : Math.Min(1, Done / Need);

        public JObj ToJson() => new JObj
        {
            ["kind"] = Kind, ["need"] = Need, ["done"] = Done, ["due"] = (long)Due, ["slot"] = (long)Slot, ["staff"] = (long)Staff, ["late"] = Late, ["fin"] = Finished,
        };

        public static TaskState Read(object o) => new TaskState
        {
            Kind = J.S(o, "kind", Kinds.File), Need = Math.Max(0, JW.D(o, "need")), Done = Math.Max(0, JW.D(o, "done")), Due = J.I(o, "due", 1), Slot = J.I(o, "slot"),
            Staff = J.I(o, "staff", Attend.None), Late = J.B(o, "late"), Finished = J.B(o, "fin"),
        };
    }

    public sealed class MatterState
    {
        public int Id;
        public string Area = "traffic", Tier = "S", Fee = Fees.Hourly;
        public string Status = MatterStatus.Upcoming;
        public int Title;                // matter.<area>.<n>
        public int Client, Opponent;     // party indices
        public int Judge;                // judge index
        public double Twist;             // hidden difficulty
        public bool Revealed;            // twist known (evidence done / case preview)
        public bool Wall;                // accepted behind an ethical wall
        public bool Continued;           // continuance used (once)
        public bool Tutorial;            // the guide's scripted matter
        public int AcceptDay;
        public double Expires;           // intake: day minute the client walks out
        public double Trust;             // client money held for this matter (retainers)
        public double Billed;            // career total billed on this matter
        public double HearingEnd;        // in session: day minute the verdict lands
        public double NoLawyer;          // in session without a lawyer (penalty applied)
        public int Claims = 2;
        public List<int> Exhibits = new List<int>();   // prep table: exhibit order shown (exhibit e proves claim e; e == Claims = decoy)
        public List<int> Matched = new List<int>();    // claims matched at the prep table
        public List<TaskState> Tasks = new List<TaskState>();

        public TaskState HearingTask => Tasks.Find(t => t.Kind == Kinds.Hearing);
        public TaskState Task(string kind) => Tasks.Find(t => t.Kind == kind);
        public bool Active => Status == MatterStatus.Open || Status == MatterStatus.Hearing;
        public bool PuzzleSolved => Matched.Count >= Claims;

        public JObj ToJson()
        {
            var ts = new List<object>(); foreach (var t in Tasks) ts.Add(t.ToJson());
            return new JObj
            {
                ["id"] = (long)Id, ["area"] = Area, ["tier"] = Tier, ["fee"] = Fee, ["status"] = Status, ["title"] = (long)Title, ["client"] = (long)Client,
                ["opp"] = (long)Opponent, ["judge"] = (long)Judge, ["twist"] = Twist, ["revealed"] = Revealed, ["wall"] = Wall, ["cont"] = Continued,
                ["tut"] = Tutorial, ["accept"] = (long)AcceptDay, ["expires"] = Expires, ["trust"] = Trust, ["billed"] = Billed, ["hearing_end"] = HearingEnd,
                ["no_lawyer"] = NoLawyer, ["claims"] = (long)Claims, ["exhibits"] = JW.Arr(Exhibits), ["matched"] = JW.Arr(Matched), ["tasks"] = ts,
            };
        }

        public static MatterState Read(object o)
        {
            var m = new MatterState
            {
                Id = J.I(o, "id"), Area = J.S(o, "area", "traffic"), Tier = J.S(o, "tier", "S"), Fee = J.S(o, "fee", Fees.Hourly), Status = J.S(o, "status", MatterStatus.Upcoming),
                Title = J.I(o, "title"), Client = J.I(o, "client"), Opponent = J.I(o, "opp"), Judge = J.I(o, "judge"), Twist = JW.D(o, "twist"), Revealed = J.B(o, "revealed"),
                Wall = J.B(o, "wall"), Continued = J.B(o, "cont"), Tutorial = J.B(o, "tut"), AcceptDay = J.I(o, "accept"), Expires = JW.D(o, "expires"),
                Trust = Math.Max(0, JW.D(o, "trust")), Billed = JW.D(o, "billed"), HearingEnd = JW.D(o, "hearing_end"), NoLawyer = JW.D(o, "no_lawyer"),
                Claims = J.I(o, "claims", 2), Exhibits = JW.Ints(o, "exhibits"), Matched = JW.Ints(o, "matched"),
            };
            foreach (var t in J.Arr(o, "tasks") ?? new List<object>()) m.Tasks.Add(TaskState.Read(t));
            return m;
        }
    }

    public sealed class RoomState
    {
        public string Id;
        public int Count;                // built (desks stack)
        public double BuildLeftS;        // real seconds left on the one running build (0 = none)
        public JObj ToJson() => new JObj { ["id"] = Id, ["n"] = (long)Count, ["left"] = BuildLeftS };
        public static RoomState Read(object o) => new RoomState { Id = J.S(o, "id"), Count = Math.Max(0, J.I(o, "n")), BuildLeftS = Math.Max(0, JW.D(o, "left")) };
    }

    public sealed class ActiveEvent
    {
        public string Id;
        public int UntilDay;
        public int Target = -1;
        public double From, To;          // day minutes
        public JObj ToJson() => new JObj { ["id"] = Id, ["until"] = (long)UntilDay, ["target"] = (long)Target, ["from"] = From, ["to"] = To };
        public static ActiveEvent Read(object o) => new ActiveEvent { Id = J.S(o, "id"), UntilDay = J.I(o, "until"), Target = J.I(o, "target", -1), From = JW.D(o, "from"), To = JW.D(o, "to") };
    }

    public sealed class PlannedEvent
    {
        public string Id;                // a crisis id, or "walkin"
        public double At;
        public JObj ToJson() => new JObj { ["id"] = Id, ["at"] = At };
        public static PlannedEvent Read(object o) => new PlannedEvent { Id = J.S(o, "id"), At = JW.D(o, "at") };
    }

    public sealed class DayStats
    {
        public int Day;
        public int Walkins, Accepted, Declined, Referred, WalkedOut, Wins, Losses, Settles, Late, NoLawyer, Continuances, Hearings;
        public double HoursWorked, HoursAvail, Billed, Fees, Boost, Salaries, Rent, Visiting, Sanctions, Walls, Invoiced;
        public int StarDeadlines, StarClients, StarBilling;
        public double Income => Billed + Fees + Boost;
        public double Costs => Salaries + Rent + Visiting + Sanctions + Walls;
        public double Net => Income - Costs;
        public int Stars => StarDeadlines + StarClients + StarBilling;
        public double Utilization => HoursAvail <= 0 ? 0 : Math.Min(1, HoursWorked / HoursAvail);

        public JObj ToJson() => new JObj
        {
            ["day"] = (long)Day, ["walkins"] = (long)Walkins, ["acc"] = (long)Accepted, ["dec"] = (long)Declined, ["ref"] = (long)Referred, ["out"] = (long)WalkedOut,
            ["wins"] = (long)Wins, ["losses"] = (long)Losses, ["settles"] = (long)Settles, ["late"] = (long)Late, ["no_lawyer"] = (long)NoLawyer,
            ["cont"] = (long)Continuances, ["hearings"] = (long)Hearings, ["worked"] = HoursWorked, ["avail"] = HoursAvail, ["billed"] = Billed, ["fees"] = Fees,
            ["boost"] = Boost, ["salaries"] = Salaries, ["rent"] = Rent, ["visiting"] = Visiting, ["sanctions"] = Sanctions, ["walls"] = Walls, ["invoiced"] = Invoiced,
            ["s1"] = (long)StarDeadlines, ["s2"] = (long)StarClients, ["s3"] = (long)StarBilling,
        };

        public static DayStats Read(object o) => new DayStats
        {
            Day = J.I(o, "day"), Walkins = J.I(o, "walkins"), Accepted = J.I(o, "acc"), Declined = J.I(o, "dec"), Referred = J.I(o, "ref"), WalkedOut = J.I(o, "out"),
            Wins = J.I(o, "wins"), Losses = J.I(o, "losses"), Settles = J.I(o, "settles"), Late = J.I(o, "late"), NoLawyer = J.I(o, "no_lawyer"),
            Continuances = J.I(o, "cont"), Hearings = J.I(o, "hearings"), HoursWorked = JW.D(o, "worked"), HoursAvail = JW.D(o, "avail"), Billed = JW.D(o, "billed"),
            Fees = JW.D(o, "fees"), Boost = JW.D(o, "boost"), Salaries = JW.D(o, "salaries"), Rent = JW.D(o, "rent"), Visiting = JW.D(o, "visiting"),
            Sanctions = JW.D(o, "sanctions"), Walls = JW.D(o, "walls"), Invoiced = JW.D(o, "invoiced"), StarDeadlines = J.I(o, "s1"), StarClients = J.I(o, "s2"), StarBilling = J.I(o, "s3"),
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
        public double Minute = 540;
        public string Phase = Phases.Plan;
        public int Speed = 1;
        public bool Paused;
        public double Cash;               // operating account
        public double Wip;                // work in progress: today's invoices + fees, not billed yet (billing run)
        public bool Billed;               // today's billing run done
        public bool Boosted;              // today's billing boost (rewarded) used
        public int Chapter = 1;
        public int NextId = 1;            // matters
        public int NextStaff;             // staff ids
        public double[] Standing = new double[0];   // per area (Gd.Areas order)
        public List<StaffState> Staff = new List<StaffState>();
        public List<MatterState> Matters = new List<MatterState>();   // upcoming + intake + open + hearing
        public List<RoomState> Rooms = new List<RoomState>();
        public string Building = "";      // room id being built
        public List<string> Rules = new List<string>();    // standing instructions: auto-accept S matters in these areas
        public List<ActiveEvent> Events = new List<ActiveEvent>();
        public List<PlannedEvent> Planned = new List<PlannedEvent>();
        public DayStats Today = new DayStats { Day = 1 };
        public List<DayStats> History = new List<DayStats>();
        public bool Tutorial = true;      // guide running: no crises, no sanctions, hearings cannot be lost
        public int TutorialMatter;        // the guide's matter id (0 = none)
        public List<int> TutorialCollision = new List<int>();   // the guide's two collision matters
        public Dictionary<string, RewardUse> Rewards = new Dictionary<string, RewardUse>();
        public double OfflinePending;
        public long LastSeen;
        public int Closed, Wins, Losses, Settles, BestDayStars;
        public double CareerFees;
        public List<string> Seen = new List<string>();   // sim-side first-time markers

        public JObj ToJson()
        {
            var st = new List<object>(); foreach (var s in Staff) st.Add(s.ToJson());
            var ms = new List<object>(); foreach (var m in Matters) ms.Add(m.ToJson());
            var rs = new List<object>(); foreach (var r in Rooms) rs.Add(r.ToJson());
            var ev = new List<object>(); foreach (var e in Events) ev.Add(e.ToJson());
            var pl = new List<object>(); foreach (var e in Planned) pl.Add(e.ToJson());
            var hi = new List<object>(); foreach (var h in History) hi.Add(h.ToJson());
            var rw = new JObj(); foreach (var kv in Rewards) rw[kv.Key] = kv.Value.ToJson();
            return new JObj
            {
                ["game"] = "docket-day", ["v"] = (long)Version, ["seed"] = Seed, ["rng"] = RngState, ["day"] = (long)Day, ["minute"] = Minute, ["phase"] = Phase,
                ["speed"] = (long)Speed, ["paused"] = Paused, ["cash"] = Cash, ["wip"] = Wip, ["billed"] = Billed, ["boosted"] = Boosted, ["chapter"] = (long)Chapter,
                ["next_id"] = (long)NextId, ["next_staff"] = (long)NextStaff, ["standing"] = JW.Arr(Standing), ["staff"] = st, ["matters"] = ms, ["rooms"] = rs,
                ["building"] = Building, ["rules"] = JW.Arr(Rules), ["events"] = ev, ["planned"] = pl, ["today"] = Today.ToJson(), ["history"] = hi,
                ["tutorial"] = Tutorial, ["tut_matter"] = (long)TutorialMatter, ["tut_collision"] = JW.Arr(TutorialCollision), ["rewards"] = rw,
                ["offline_pending"] = OfflinePending, ["last_seen"] = LastSeen, ["closed"] = (long)Closed, ["wins"] = (long)Wins, ["losses"] = (long)Losses,
                ["settles"] = (long)Settles, ["best_stars"] = (long)BestDayStars, ["career_fees"] = CareerFees, ["seen"] = JW.Arr(Seen),
            };
        }
    }
}
