// Pour & Roast sim core — engine independent. The whole saved career (State) and its parts. Every class writes and reads
// itself (ToJson / Read); reads are lenient (defaults for missing fields) so older saves load (fleet §14: the codec accepts
// every state the sim can produce — negative cash, a ticket mid-shot, a jammed grinder, a batch in the drum, the report
// screen, a tutorial step).
using System;
using System.Collections.Generic;

namespace PourAndRoast.Sim
{
    public static class Phases
    {
        public const string Plan = "plan", Live = "live", Report = "report";
        public static bool Valid(string p) => p == Plan || p == Live || p == Report;
    }

    /// <summary>The player-set order the bar works the ticket rail in (design §5.3: priority rules are player-set).</summary>
    public static class Rules
    {
        public const string Fifo = "fifo", Deadline = "deadline", Vip = "vip";
        public static readonly string[] All = { Fifo, Deadline, Vip };
        public static bool Valid(string r) => r == Fifo || r == Deadline || r == Vip;
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
        public static int[] Ints(object o, string key, int len)
        {
            var r = new int[len];
            var a = J.Arr(o, key);
            if (a != null) for (int i = 0; i < len && i < a.Count; i++) r[i] = J.I(a[i]);
            return r;
        }
        public static double Finite(double d) => double.IsNaN(d) || double.IsInfinity(d) ? 0 : d;
        public static double D(object o, string key, double def = 0) => Finite(J.D(o, key, def));
    }

    /// <summary>One drink / pastry of a ticket. Stage = index of its next task in the menu item's task list; Worker = the
    /// staff id working it now (-1 none, -2 the rewarded helper).</summary>
    public sealed class ItemState
    {
        public int M;                 // menu index
        public int Milk;              // 0 none, 1 whole, 2 oat
        public int Stage;
        public int Worker = -1;
        public double Left;           // game minutes left of the running task
        public double Q = 70;
        public bool Failed, Sub;      // sold out / out of milk; milk substituted
        public bool Batched;

        public JObj ToJson() => new JObj
        {
            ["m"] = (long)M, ["milk"] = (long)Milk, ["stage"] = (long)Stage, ["w"] = (long)Worker, ["left"] = Left, ["q"] = Q, ["failed"] = Failed, ["sub"] = Sub, ["batch"] = Batched,
        };

        public static ItemState Read(object o) => new ItemState
        {
            M = J.I(o, "m"), Milk = J.I(o, "milk"), Stage = J.I(o, "stage"), Worker = J.I(o, "w", -1), Left = JW.D(o, "left"), Q = JW.D(o, "q", 70),
            Failed = J.B(o, "failed"), Sub = J.B(o, "sub"), Batched = J.B(o, "batch"),
        };
    }

    public sealed class TicketState
    {
        public int Id;
        public int Cust;              // customer type index
        public bool Pre, Flag, Ready, Dup;
        public double Arrived, Due, ReadyAt;
        public List<ItemState> Items = new List<ItemState>();

        public JObj ToJson()
        {
            var it = new List<object>(); foreach (var i in Items) it.Add(i.ToJson());
            return new JObj
            {
                ["id"] = (long)Id, ["cust"] = (long)Cust, ["pre"] = Pre, ["flag"] = Flag, ["ready"] = Ready, ["dup"] = Dup, ["arr"] = Arrived, ["due"] = Due,
                ["ready_at"] = ReadyAt, ["items"] = it,
            };
        }

        public static TicketState Read(object o)
        {
            var t = new TicketState
            {
                Id = J.I(o, "id"), Cust = J.I(o, "cust"), Pre = J.B(o, "pre"), Flag = J.B(o, "flag"), Ready = J.B(o, "ready"), Dup = J.B(o, "dup"),
                Arrived = JW.D(o, "arr"), Due = JW.D(o, "due"), ReadyAt = JW.D(o, "ready_at"),
            };
            foreach (var i in J.Arr(o, "items") ?? new List<object>()) t.Items.Add(ItemState.Read(i));
            return t;
        }
    }

    public sealed class StaffState
    {
        public int Id;
        public string Role = Roles.Barista, Station = Stations.Any, Trait = "steady", Venue = "cart";
        public int Level = 1;
        public JObj ToJson() => new JObj { ["id"] = (long)Id, ["role"] = Role, ["station"] = Station, ["trait"] = Trait, ["venue"] = Venue, ["level"] = (long)Level };
        public static StaffState Read(object o)
        {
            var s = new StaffState { Id = J.I(o, "id"), Role = J.S(o, "role", Roles.Barista), Station = J.S(o, "station", Stations.Any), Trait = J.S(o, "trait", "steady"), Venue = J.S(o, "venue", "cart"), Level = J.I(o, "level", 1) };
            if (!Stations.Valid(s.Station)) s.Station = Stations.Any;
            if (s.Level < 1) s.Level = 1;
            return s;
        }
    }

    /// <summary>An owned branch (no-reset rule: it keeps its equipment when the player works another venue).</summary>
    public sealed class VenueState
    {
        public string Id = "cart";
        public int Heads = 1, Wands = 1, Grinder = 1, Ice, Rail, Seats, Decor;
        public double Tune = 100, Wear, JamUntil = -1;
        public bool Jammed;
        public double BestNet;
        public int DaysRun;

        public JObj ToJson() => new JObj
        {
            ["id"] = Id, ["heads"] = (long)Heads, ["wands"] = (long)Wands, ["grinder"] = (long)Grinder, ["ice"] = (long)Ice, ["rail"] = (long)Rail, ["seats"] = (long)Seats,
            ["decor"] = (long)Decor, ["tune"] = Tune, ["wear"] = Wear, ["jam_until"] = JamUntil, ["jammed"] = Jammed, ["best_net"] = BestNet, ["days"] = (long)DaysRun,
        };

        public static VenueState Read(object o) => new VenueState
        {
            Id = J.S(o, "id", "cart"), Heads = J.I(o, "heads", 1), Wands = J.I(o, "wands", 1), Grinder = J.I(o, "grinder", 1), Ice = J.I(o, "ice"), Rail = J.I(o, "rail"),
            Seats = J.I(o, "seats"), Decor = J.I(o, "decor"), Tune = JW.D(o, "tune", 100), Wear = JW.D(o, "wear"), JamUntil = JW.D(o, "jam_until", -1), Jammed = J.B(o, "jammed"),
            BestNet = JW.D(o, "best_net"), DaysRun = J.I(o, "days"),
        };
    }

    /// <summary>A roast batch: in the drum (Progress 0..1, real seconds), then resting (ReadyDay), then blended.</summary>
    public sealed class BatchState
    {
        public string Origin = "", Profile = Profiles.Medium;
        public double Kg, Progress, Q;
        public int ReadyDay;
        public JObj ToJson() => new JObj { ["origin"] = Origin, ["profile"] = Profile, ["kg"] = Kg, ["progress"] = Progress, ["q"] = Q, ["ready"] = (long)ReadyDay };
        public static BatchState Read(object o)
        {
            var b = new BatchState { Origin = J.S(o, "origin"), Profile = J.S(o, "profile", Profiles.Medium), Kg = JW.D(o, "kg"), Progress = JW.D(o, "progress"), Q = JW.D(o, "q"), ReadyDay = J.I(o, "ready") };
            if (!Profiles.Valid(b.Profile)) b.Profile = Profiles.Medium;
            return b;
        }
    }

    public sealed class ContractState
    {
        public string Id = "";
        public bool Active;
        public int NextDay;
        public double PayMult = 1;
        public int Delivered, Missed;
        public JObj ToJson() => new JObj { ["id"] = Id, ["active"] = Active, ["next"] = (long)NextDay, ["pay"] = PayMult, ["ok"] = (long)Delivered, ["miss"] = (long)Missed };
        public static ContractState Read(object o) => new ContractState { Id = J.S(o, "id"), Active = J.B(o, "active"), NextDay = J.I(o, "next"), PayMult = JW.D(o, "pay", 1), Delivered = J.I(o, "ok"), Missed = J.I(o, "miss") };
    }

    public sealed class ActiveEvent
    {
        public string Id;
        public int UntilDay;          // last day it applies (inclusive)
        public int Target = -1;       // origin index (frost)
        public double Mult = 1;
        public JObj ToJson() => new JObj { ["id"] = Id, ["until"] = (long)UntilDay, ["target"] = (long)Target, ["mult"] = Mult };
        public static ActiveEvent Read(object o) => new ActiveEvent { Id = J.S(o, "id"), UntilDay = J.I(o, "until"), Target = J.I(o, "target", -1), Mult = JW.D(o, "mult", 1) };
    }

    /// <summary>A card on the Review Wall (no names, no free text: a customer type, stars and a reason key).</summary>
    public sealed class ReviewCard
    {
        public int Day, Cust, Stars;
        public string Reason = "";
        public JObj ToJson() => new JObj { ["day"] = (long)Day, ["cust"] = (long)Cust, ["stars"] = (long)Stars, ["why"] = Reason };
        public static ReviewCard Read(object o) => new ReviewCard { Day = J.I(o, "day"), Cust = J.I(o, "cust"), Stars = J.I(o, "stars", 3), Reason = J.S(o, "why") };
    }

    public sealed class DayStats
    {
        public int Day;
        public string Venue = "", Weather = "";
        public int Customers, Served, Walked, Balked, Items, Failed, PreOrders, Late, Shots, ReviewN;
        public double ReviewSum, WaitSum, QSum, MilkUsedW, MilkUsedO;
        public double Revenue, Tips, Wholesale, Branches, Bonus, BeansCost, MilkCost, PastryCost, IceCost, Wages, Rent, Fees, Waste;
        public double Income => Revenue + Tips + Wholesale + Branches + Bonus;
        public double Costs => BeansCost + MilkCost + PastryCost + IceCost + Wages + Rent + Fees;
        public double Net => Income - Costs;
        public double AvgReview => ReviewN <= 0 ? 0 : ReviewSum / ReviewN;
        public double WalkRate => Customers <= 0 ? 0 : (double)(Walked + Balked) / Customers;
        public double AvgQ => Items - Failed <= 0 ? 0 : QSum / Math.Max(1, Items - Failed);
        public double OnTime => PreOrders <= 0 ? 1 : 1.0 - (double)Late / PreOrders;
        /// <summary>The report's day stars (1-5): the rounded average review, 0 when nobody was served.</summary>
        public int Stars => ReviewN <= 0 ? 0 : Math.Max(1, Math.Min(5, (int)Math.Round(AvgReview)));

        public JObj ToJson() => new JObj
        {
            ["day"] = (long)Day, ["venue"] = Venue, ["weather"] = Weather, ["cust"] = (long)Customers, ["served"] = (long)Served, ["walked"] = (long)Walked, ["balked"] = (long)Balked,
            ["items"] = (long)Items, ["failed"] = (long)Failed, ["pre"] = (long)PreOrders, ["late"] = (long)Late, ["shots"] = (long)Shots, ["rev_n"] = (long)ReviewN,
            ["rev_sum"] = ReviewSum, ["wait_sum"] = WaitSum, ["q_sum"] = QSum, ["milk_w"] = MilkUsedW, ["milk_o"] = MilkUsedO, ["revenue"] = Revenue, ["tips"] = Tips, ["wholesale"] = Wholesale, ["branches"] = Branches,
            ["bonus"] = Bonus, ["beans"] = BeansCost, ["milk"] = MilkCost, ["pastry"] = PastryCost, ["ice"] = IceCost, ["wages"] = Wages, ["rent"] = Rent, ["fees"] = Fees, ["waste"] = Waste,
        };

        public static DayStats Read(object o) => new DayStats
        {
            Day = J.I(o, "day"), Venue = J.S(o, "venue"), Weather = J.S(o, "weather"), Customers = J.I(o, "cust"), Served = J.I(o, "served"), Walked = J.I(o, "walked"),
            Balked = J.I(o, "balked"), Items = J.I(o, "items"), Failed = J.I(o, "failed"), PreOrders = J.I(o, "pre"), Late = J.I(o, "late"), Shots = J.I(o, "shots"),
            ReviewN = J.I(o, "rev_n"), ReviewSum = JW.D(o, "rev_sum"), WaitSum = JW.D(o, "wait_sum"), QSum = JW.D(o, "q_sum"), MilkUsedW = JW.D(o, "milk_w"), MilkUsedO = JW.D(o, "milk_o"), Revenue = JW.D(o, "revenue"), Tips = JW.D(o, "tips"),
            Wholesale = JW.D(o, "wholesale"), Branches = JW.D(o, "branches"), Bonus = JW.D(o, "bonus"), BeansCost = JW.D(o, "beans"), MilkCost = JW.D(o, "milk"),
            PastryCost = JW.D(o, "pastry"), IceCost = JW.D(o, "ice"), Wages = JW.D(o, "wages"), Rent = JW.D(o, "rent"), Fees = JW.D(o, "fees"), Waste = JW.D(o, "waste"),
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

    /// <summary>The saved career. Arrays indexed by data order (menu, origins) are written as id-keyed objects.</summary>
    public sealed class State
    {
        public const int Version = 1;
        public long Seed = 1, RngState = 1;
        public int Day = 1;
        public double Minute = 420;
        public string Phase = Phases.Plan;
        public int Speed = 1;
        public bool Paused, Tutorial = true;
        public double Cash, Rating = 2.5;
        // business
        public string Venue = "cart";
        public List<VenueState> Venues = new List<VenueState>();
        public List<StaffState> Staff = new List<StaffState>();
        public int NextStaffId, NextTicketId = 1;
        public List<TicketState> Tickets = new List<TicketState>();
        public string Rule = Rules.Fifo;
        public bool Batch;
        public List<string> Menu = new List<string>();
        public int[] Par = new int[0];            // per menu item (pastries)
        public int[] Pastry = new int[0];         // pastry stock per menu item
        // stocks
        public double[] Milk = { 0, 0 }, MilkOrder = { 18, 6 }, MilkPending = { 0, 0 };
        public double MilkArriveAt = -1, RunArriveAt = -1;
        public double[] RunPending = { 0, 0 };
        public string Supplier = "regular";
        public double Ice;
        public int PrepHot;
        public bool AutoBeans = true;
        public double BeanKg, BeanQ = 52;
        // roastery
        public double[] Green = new double[0], Market = new double[0];
        public int Drum = 1;
        public BatchState Roast;
        public List<BatchState> Resting = new List<BatchState>();
        public List<ContractState> Contracts = new List<ContractState>();
        // world
        public List<string> Forecast = new List<string>();
        public List<ActiveEvent> Events = new List<ActiveEvent>();
        public List<ReviewCard> Reviews = new List<ReviewCard>();
        public int LowStreak;
        public DayStats Today = new DayStats { Day = 1 };
        public List<DayStats> History = new List<DayStats>();
        // research
        public List<string> Research = new List<string>();
        public string Researching = "";
        public double ResearchLeftS;
        // rewards / offline
        public Dictionary<string, RewardUse> Rewards = new Dictionary<string, RewardUse>();
        public double OfflinePending, HelperUntil = -1, AutoTuneUntil = -1;
        public long LastSeen;
        public int BestDayStars, FiveStarDays;
        public long Served;               // career total
        public List<string> Seen = new List<string>();

        public VenueState Active => Venues.Find(v => v.Id == Venue) ?? (Venues.Count > 0 ? Venues[0] : null);
        public VenueState VenueOf(string id) => Venues.Find(v => v.Id == id);

        public JObj ToJson(GameData gd)
        {
            var vs = new List<object>(); foreach (var v in Venues) vs.Add(v.ToJson());
            var st = new List<object>(); foreach (var s in Staff) st.Add(s.ToJson());
            var tk = new List<object>(); foreach (var t in Tickets) tk.Add(t.ToJson());
            var rs = new List<object>(); foreach (var b in Resting) rs.Add(b.ToJson());
            var ct = new List<object>(); foreach (var c in Contracts) ct.Add(c.ToJson());
            var ev = new List<object>(); foreach (var e in Events) ev.Add(e.ToJson());
            var rv = new List<object>(); foreach (var r in Reviews) rv.Add(r.ToJson());
            var hi = new List<object>(); foreach (var h in History) hi.Add(h.ToJson());
            var rw = new JObj(); foreach (var kv in Rewards) rw[kv.Key] = kv.Value.ToJson();
            var par = new JObj(); var pastry = new JObj();
            for (int i = 0; i < gd.Menu.Count; i++)
                if (gd.Menu[i].IsPastry) { par[gd.Menu[i].Id] = (long)(i < Par.Length ? Par[i] : 0); pastry[gd.Menu[i].Id] = (long)(i < Pastry.Length ? Pastry[i] : 0); }
            var green = new JObj(); var market = new JObj();
            for (int i = 0; i < gd.Origins.Count; i++) { green[gd.Origins[i].Id] = i < Green.Length ? Green[i] : 0.0; market[gd.Origins[i].Id] = i < Market.Length ? Market[i] : 1.0; }
            return new JObj
            {
                ["game"] = "pour-and-roast", ["v"] = (long)Version, ["seed"] = Seed, ["rng"] = RngState, ["day"] = (long)Day, ["minute"] = Minute, ["phase"] = Phase,
                ["speed"] = (long)Speed, ["paused"] = Paused, ["tutorial"] = Tutorial, ["cash"] = Cash, ["rating"] = Rating,
                ["venue"] = Venue, ["venues"] = vs, ["staff"] = st, ["next_staff"] = (long)NextStaffId, ["next_ticket"] = (long)NextTicketId, ["tickets"] = tk,
                ["rule"] = Rule, ["batch"] = Batch, ["menu"] = JW.Arr(Menu), ["par"] = par, ["pastry"] = pastry,
                ["milk"] = JW.Arr(Milk), ["milk_order"] = JW.Arr(MilkOrder), ["milk_pending"] = JW.Arr(MilkPending), ["milk_at"] = MilkArriveAt,
                ["run_at"] = RunArriveAt, ["run_pending"] = JW.Arr(RunPending), ["supplier"] = Supplier, ["ice"] = Ice, ["prep_hot"] = (long)PrepHot,
                ["auto_beans"] = AutoBeans, ["bean_kg"] = BeanKg, ["bean_q"] = BeanQ,
                ["green"] = green, ["market"] = market, ["drum"] = (long)Drum, ["roast"] = Roast != null ? Roast.ToJson() : null, ["resting"] = rs, ["contracts"] = ct,
                ["forecast"] = JW.Arr(Forecast), ["events"] = ev, ["reviews"] = rv, ["low_streak"] = (long)LowStreak, ["today"] = Today.ToJson(), ["history"] = hi,
                ["research"] = JW.Arr(Research), ["researching"] = Researching, ["research_left"] = ResearchLeftS,
                ["rewards"] = rw, ["offline_pending"] = OfflinePending, ["helper_until"] = HelperUntil, ["autotune_until"] = AutoTuneUntil, ["last_seen"] = LastSeen,
                ["best_stars"] = (long)BestDayStars, ["five_star_days"] = (long)FiveStarDays, ["served"] = Served, ["seen"] = JW.Arr(Seen),
            };
        }
    }
}
