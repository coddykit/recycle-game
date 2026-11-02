// Volt City sim core — engine independent (no UnityEngine). C# 9 / .NET Standard 2.1.
using System.Collections.Generic;

namespace VoltCity.Sim
{
    public sealed class ShiftReport
    {
        public int Shift, UptimePm, Stars, Co2Stars, OutageMin, Shed, Faults, Repairs, Trips, BandPm, Negative, RatingX100, Chapter;
        public long ServedMwh, DemandMwh, Revenue, Fuel, Upkeep, Bonus, Profit, PriceMilli, RewardBonus;
        public string Weather = "", Hash = "";
        public List<string> Events = new List<string>();

        public JObj ToJson()
        {
            var ev = new List<object>();
            foreach (var e in Events) ev.Add(e);
            return new JObj
            {
                ["shift"] = (long)Shift, ["uptime_pm"] = (long)UptimePm, ["served_mwh"] = ServedMwh, ["demand_mwh"] = DemandMwh, ["revenue"] = Revenue,
                ["fuel"] = Fuel, ["upkeep"] = Upkeep, ["bonus"] = Bonus, ["profit"] = Profit, ["stars"] = (long)Stars, ["co2_stars"] = (long)Co2Stars,
                ["outage_min"] = (long)OutageMin, ["shed"] = (long)Shed, ["faults"] = (long)Faults, ["repairs"] = (long)Repairs, ["trips"] = (long)Trips,
                ["band_pm"] = (long)BandPm, ["events"] = ev, ["negative"] = (long)Negative, ["price_milli"] = PriceMilli, ["reward_bonus"] = RewardBonus,
                ["rating_x100"] = (long)RatingX100, ["chapter"] = (long)Chapter, ["weather"] = Weather, ["hash"] = Hash
            };
        }

        public static ShiftReport FromJson(object o)
        {
            if (!(o is JObj)) return null;
            return new ShiftReport
            {
                Shift = J.I(o, "shift"), UptimePm = J.I(o, "uptime_pm"), ServedMwh = J.L(o, "served_mwh"), DemandMwh = J.L(o, "demand_mwh"), Revenue = J.L(o, "revenue"),
                Fuel = J.L(o, "fuel"), Upkeep = J.L(o, "upkeep"), Bonus = J.L(o, "bonus"), Profit = J.L(o, "profit"), Stars = J.I(o, "stars"), Co2Stars = J.I(o, "co2_stars"),
                OutageMin = J.I(o, "outage_min"), Shed = J.I(o, "shed"), Faults = J.I(o, "faults"), Repairs = J.I(o, "repairs"), Trips = J.I(o, "trips"),
                BandPm = J.I(o, "band_pm"), Events = J.StrList(o, "events"), Negative = J.I(o, "negative"), PriceMilli = J.L(o, "price_milli"),
                RewardBonus = J.L(o, "reward_bonus"), RatingX100 = J.I(o, "rating_x100"), Chapter = J.I(o, "chapter"), Weather = J.S(o, "weather"), Hash = J.S(o, "hash")
            };
        }
    }

    public sealed class OfflineSummary
    {
        public long AwayS, CountedS, Cash;
        public bool Capped, Doubled;
        public int AwayDays;

        public JObj ToJson() => new JObj
        {
            ["away_s"] = AwayS, ["counted_s"] = CountedS, ["cash"] = Cash, ["capped"] = Capped, ["away_days"] = (long)AwayDays, ["doubled"] = Doubled
        };

        public static OfflineSummary FromJson(object o)
        {
            if (!(o is JObj j) || j.Count == 0) return null;
            return new OfflineSummary
            {
                AwayS = J.L(o, "away_s"), CountedS = J.L(o, "counted_s"), Cash = J.L(o, "cash"), Capped = J.B(o, "capped"), AwayDays = J.I(o, "away_days"), Doubled = J.B(o, "doubled")
            };
        }
    }

    public sealed class Stats
    {
        public long Shifts, PlayUs, Earned, Spent, Purchases, Offline, Rewarded, BoughtFunds, Stars3;

        public JObj ToJson() => new JObj
        {
            ["shifts"] = Shifts, ["play_us"] = PlayUs, ["earned"] = Earned, ["spent"] = Spent, ["purchases"] = Purchases, ["offline"] = Offline,
            ["rewarded"] = Rewarded, ["bought_funds"] = BoughtFunds, ["stars3"] = Stars3
        };

        public static Stats FromJson(object o) => new Stats
        {
            Shifts = J.L(o, "shifts"), PlayUs = J.L(o, "play_us"), Earned = J.L(o, "earned"), Spent = J.L(o, "spent"), Purchases = J.L(o, "purchases"),
            Offline = J.L(o, "offline"), Rewarded = J.L(o, "rewarded"), BoughtFunds = J.L(o, "bought_funds"), Stars3 = J.L(o, "stars3")
        };
    }

    /// <summary>Rewarded-ad bookkeeping inside the career save (atomic with the cash it changes; CLAUDE section 4 + 14).</summary>
    public sealed class RewardState
    {
        public long Day = -1, CashToday;
        public int BonusShift = -1;
        public Dictionary<string, int> N = new Dictionary<string, int>();
        public Dictionary<string, long> Last = new Dictionary<string, long>();

        public JObj ToJson()
        {
            var n = new JObj();
            foreach (var kv in N) n[kv.Key] = (long)kv.Value;
            var l = new JObj();
            foreach (var kv in Last) l[kv.Key] = kv.Value;
            return new JObj { ["day"] = Day, ["n"] = n, ["last"] = l, ["cash_today"] = CashToday, ["bonus_shift"] = (long)BonusShift };
        }

        public static RewardState FromJson(object o)
        {
            var r = new RewardState { Day = J.L(o, "day", -1), CashToday = J.L(o, "cash_today"), BonusShift = J.I(o, "bonus_shift", -1) };
            var n = J.Obj(o, "n");
            if (n != null) foreach (var k in n.Keys) r.N[k] = J.I(n[k]);
            var l = J.Obj(o, "last");
            if (l != null) foreach (var k in l.Keys) r.Last[k] = J.L(l[k]);
            return r;
        }
    }

    /// <summary>The whole career save (JSON safe: integers, strings, bools; the real-time clock is the only double).</summary>
    public sealed class CareerState
    {
        public const int Schema = 1;
        public static readonly string[] Phases = { "idle", "live", "report" };

        public long Seed = 1, AccUs, Cash, LastNet, LastSeen;
        public double Clock;
        public string Phase = "idle", WxPrev = "";
        public int ShiftNo, Chapter = 1, ChapterShift, RatingX100 = 250, Crews = 1, CrewSpeed, TreeTrim, Dr, WeatherStation, HeatLeft, HeatPct, LastStars = 3;
        public int Speed = 1, Ch5Good, Ch5Done = -1;
        public bool Paused, TutorialSafe;
        public List<int> RatingHist = new List<int>();
        public Dictionary<string, int> Owned = new Dictionary<string, int>();       // investment -> shift bought
        public Dictionary<string, int> Plants = new Dictionary<string, int>();
        public Dictionary<string, int> Lines = new Dictionary<string, int>();
        public List<string> Ties = new List<string>();
        public HashSet<string> Auto = new HashSet<string>();                         // dispatcher assistant parts owned
        public Dictionary<string, bool> AutoOn = new Dictionary<string, bool> { ["baseload"] = true, ["battery"] = true, ["crew"] = true };
        public Dictionary<string, int> Hooked = new Dictionary<string, int>();      // district -> shifts connected
        public List<string> Rush = new List<string>();
        public Carry Carry = new Carry();
        public ShiftPlan Plan;                                                        // the live / last shift
        public JObj Shift;                                                            // grid snapshot (saves only)
        public ShiftReport Report;
        public List<ShiftReport> Reports = new List<ShiftReport>();
        public List<Cmd> Pending = new List<Cmd>();
        public List<KeyValuePair<int, Cmd>> Log = new List<KeyValuePair<int, Cmd>>();
        public Stats Stats = new Stats();
        public Dictionary<string, int> Wow = new Dictionary<string, int>();
        public RewardState Rewards = new RewardState();
        public List<string> Receipts = new List<string>();
        public OfflineSummary OfflineLast;

        public bool IsHooked(string d) => Hooked.ContainsKey(d);
        public int Level(string plant) => Plants.TryGetValue(plant, out int v) ? v : 0;

        static JObj IntMap(Dictionary<string, int> m, IEnumerable<string> order)
        {
            var o = new JObj();
            foreach (var k in order) if (m.TryGetValue(k, out int v)) o[k] = (long)v;
            return o;
        }

        static List<object> StrL(IEnumerable<string> l) { var r = new List<object>(); foreach (var s in l) r.Add(s); return r; }

        /// <summary>Canonical JSON (data order for every map: same state = same text = same hash).</summary>
        public JObj ToJson(GameData gd)
        {
            var invOrder = new List<string>();
            foreach (var x in gd.Investments) invOrder.Add(x.Id);
            var dOrder = new List<string>();
            foreach (var d in gd.Districts) dOrder.Add(d.Id);
            var cOrder = new List<string>();
            foreach (var c in gd.Corridors) cOrder.Add(c.Id);
            var hist = new List<object>();
            foreach (var h in RatingHist) hist.Add((long)h);
            var auto = new List<object>();
            foreach (var a in new[] { "baseload", "battery", "crew" }) if (Auto.Contains(a)) auto.Add(a);
            var autoOn = new JObj();
            foreach (var a in new[] { "baseload", "battery", "crew" }) autoOn[a] = !AutoOn.TryGetValue(a, out bool on) || on;
            var reps = new List<object>();
            foreach (var r in Reports) reps.Add(r.ToJson());
            var pend = new List<object>();
            foreach (var c in Pending) pend.Add(c.ToJson());
            var log = new List<object>();
            foreach (var kv in Log) log.Add(new JObj { ["step"] = (long)kv.Key, ["cmd"] = kv.Value.ToJson() });
            var wow = new JObj();
            foreach (var kv in Wow) wow[kv.Key] = (long)kv.Value;
            return new JObj
            {
                ["schema"] = (long)Schema, ["seed"] = Seed, ["clock"] = Clock, ["acc_us"] = AccUs, ["phase"] = Phase, ["cash"] = Cash, ["shift_no"] = (long)ShiftNo,
                ["chapter"] = (long)Chapter, ["chapter_shift"] = (long)ChapterShift, ["rating_x100"] = (long)RatingX100, ["rating_hist"] = hist,
                ["owned"] = IntMap(Owned, invOrder), ["plants"] = IntMap(Plants, GameData.Plants), ["lines"] = IntMap(Lines, cOrder), ["ties"] = StrL(Ties),
                ["crews"] = (long)Crews, ["crew_speed"] = (long)CrewSpeed, ["tree_trim"] = (long)TreeTrim, ["dr"] = (long)Dr, ["weather_station"] = (long)WeatherStation,
                ["auto"] = auto, ["auto_on"] = autoOn, ["hooked"] = IntMap(Hooked, dOrder), ["rush"] = StrL(Rush), ["carry"] = Carry.ToJson(), ["wx_prev"] = WxPrev,
                ["heat_left"] = (long)HeatLeft, ["heat_pct"] = (long)HeatPct, ["last_stars"] = (long)LastStars, ["last_net"] = LastNet,
                ["plan"] = Plan != null ? (object)Plan.ToJson() : new JObj(), ["shift"] = Shift ?? new JObj(), ["report"] = Report != null ? (object)Report.ToJson() : new JObj(),
                ["reports"] = reps, ["pending"] = pend, ["log"] = log, ["speed"] = (long)Speed, ["paused"] = Paused, ["tutorial_safe"] = TutorialSafe,
                ["ch5_good"] = (long)Ch5Good, ["ch5_done"] = (long)Ch5Done, ["stats"] = Stats.ToJson(), ["wow"] = wow, ["rewards"] = Rewards.ToJson(),
                ["receipts"] = StrL(Receipts), ["last_seen"] = LastSeen, ["offline_last"] = OfflineLast != null ? (object)OfflineLast.ToJson() : new JObj()
            };
        }
    }
}
