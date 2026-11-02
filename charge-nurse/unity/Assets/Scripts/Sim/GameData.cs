// Charge Nurse sim core — engine independent (no UnityEngine). C# 9 / .NET Standard 2.1.
// Typed view of the data files in Assets/Resources/data (balance.json, map.json, research.json, events.json).
// The Unity layer passes a loader (name -> text); tests read the files from disk. Errors are collected, never thrown.
using System;
using System.Collections.Generic;

namespace ChargeNurse.Sim
{
    /// <summary>Severity tags (plan §4): green mild … red critical. Shapes make them colour-blind safe in the UI.</summary>
    public static class Sev
    {
        public const int Green = 0, Yellow = 1, Orange = 2, Red = 3, N = 4;
        public static readonly string[] Ids = { "green", "yellow", "orange", "red" };
    }

    /// <summary>Staff teams (plan §5: hired as teams, no individual micromanagement). Technicians belong to a test room.</summary>
    public static class Teams
    {
        public const string Nurse = "nurse", Doctor = "doctor", Porter = "porter", Cleaner = "cleaner", Clerk = "clerk", Tech = "tech";
        /// <summary>The hireable hospital-wide teams (technicians are hired per room).</summary>
        public static readonly string[] Pool = { Nurse, Doctor, Porter, Cleaner, Clerk };
        public static readonly string[] All = { Nurse, Doctor, Porter, Cleaner, Clerk, Tech };
        public static int Index(string id) => Array.IndexOf(Pool, id);
    }

    public sealed class TeamDef { public string Id; public double Wage, Price; public int Max; }
    public sealed class RoomDef { public string Id; public double Cost, ScanMin, Upkeep; }
    public sealed class WingDef { public string Id; public int Chapter; public double Reputation, Cost, Arrivals = 1; public readonly Dictionary<string, int> StartBeds = new Dictionary<string, int>(); }
    public sealed class ZoneDef
    {
        public int Index;
        public string Id, Kind, Wing, Test = "", Ward = "";
        public double X, Y, W, D;
        public int Slots;
    }
    public sealed class ResearchDef { public string Id, Branch, Wing; public double Cost, Minutes; public List<string> Requires = new List<string>(); }
    public sealed class EventDef { public string Id; public double Weight = 1, Mult = 1, Bonus; public int MinDay = 1, Days = 1, Min, Max, Minutes, Beds; public bool Announced; }
    public sealed class RewardDef { public string Id; public int PerDay; public long CooldownS; public double Days, Minutes; public int Patients; }

    public sealed class GameData
    {
        public readonly List<string> Errors = new List<string>();
        public bool Ok => Errors.Count == 0;

        // time (game minutes of the day; a live shift 07:00-19:00, the night team runs 19:00-07:00 by itself)
        public int DayStart = 420, DayEnd = 1140, HandoverFrom = 1110, FreeMaxSpeed = 2, PaidMaxSpeed = 4, NightMin = 720;
        public double GameMinPerRealS = 3, AutosaveS = 30;
        // start
        public double StartCash = 1500, StartReputation = 50;
        public readonly Dictionary<string, int> StartTeams = new Dictionary<string, int>();
        public readonly Dictionary<string, int> StartBeds = new Dictionary<string, int>();
        // severity tables (index = Sev)
        public double[] Mix = { 0.45, 0.32, 0.17, 0.06 }, Pay = { 60, 120, 220, 380 }, Supplies = { 8, 16, 34, 60 }, TreatMin = { 14, 26, 40, 55 },
            SafeWait = { 200, 90, 35, 12 }, TestP = { 0.12, 0.45, 0.7, 0.8 }, AdmitP = { 0.02, 0.12, 0.45, 0.85 }, StayMin = { 240, 420, 600, 840 },
            OrP = { 0, 0, 0.3, 0.45 }, LwbsWait = { 260, 320 };
        // arrivals
        public double ArrivalsPerHour = 2.6, NightMult = 0.35, RepMin = 0.7, RepSpan = 0.6;
        public double[] HourCurve = { 1 };
        public double[] AmbulancePerDay = { 0, 1.2, 2.5, 4 }, AmbulanceSev = { 0, 0, 0.55, 0.45 };
        public double AmbulanceAnswerMin = 12, AmbulancePayMult = 1.5, DivertRep = 0.6;
        // tasks
        public double RegisterMin = 2, TriageMin = 4, DischargeMin = 6, CleanBayMin = 12, CleanBedMin = 20, PorterBaseMin = 4, WalkMPerMin = 40,
            LiftMin = 6, OrMin = 90, FastTrackMin = 10, TriageAccuracy = 0.82;
        // staff state
        public double FatigueBusy = 0.11, FatigueIdle = 0.05, FatigueMaxSlow = 0.35, RushedKeep = 0.5, NoteDelayMin = 45;
        public int RushedNotes = 2;
        public double BurnoutLine = 0.85, BurnoutRise = 12, BurnoutFall = 8, BurnoutSlowAt = 60, BurnoutSlow = 0.1;
        public double NightEff = 0.5, NightEffTeam = 0.8;
        public readonly Dictionary<string, TeamDef> TeamDefs = new Dictionary<string, TeamDef>();
        public readonly Dictionary<string, RoomDef> Rooms = new Dictionary<string, RoomDef>();
        public readonly Dictionary<string, double> Prices = new Dictionary<string, double>();
        public readonly Dictionary<string, double> Upkeep = new Dictionary<string, double>();
        // economy
        public double WardNight = 70, RepAlpha = 0.3, PayRepMin = 0.85, PayRepSpan = 0.3, DayIncomeFloor = 700;
        public int RepFreeDays = 3;
        public double[] BadRep = { 0.4, 0.8, 1.5 };   // reputation lost per LWBS / worsened / transferred (while it counts)
        // stars
        public double[] StarCare = { 0.15, 0.08, 0.03 }, StarWaits = { 90, 45, 20 }, StarFlow = { 0.3, 0.15, 0.06 };
        // events
        public int EventFreeDays = 3;
        public double[] EventsPerDay = { 0, 0.6, 1, 1.4 };
        // offline
        public double OfflineMinS = 120, OfflineCapH = 4, OfflineCapHPlus = 12, OfflineRate = 0.5, OfflineRatePlus = 0.8, OfflineDaysPerHour = 0.5;
        public readonly Dictionary<string, RewardDef> Rewards = new Dictionary<string, RewardDef>();
        public double RewardSingleMaxDays = 0.2;

        // map
        public readonly List<WingDef> Wings = new List<WingDef>();
        public readonly List<ZoneDef> Zones = new List<ZoneDef>();
        public readonly Dictionary<string, int> ZoneIndex = new Dictionary<string, int>();
        public readonly List<int[]> Corridors = new List<int[]>();
        double[,] dist;

        public readonly List<ResearchDef> Research = new List<ResearchDef>();
        public readonly Dictionary<string, ResearchDef> ResearchById = new Dictionary<string, ResearchDef>();
        public readonly List<EventDef> Events = new List<EventDef>();

        public double Price(string id) => Prices.TryGetValue(id, out var p) ? p : 0;
        public double UpkeepOf(string id) => Upkeep.TryGetValue(id, out var p) ? p : 0;
        public WingDef Wing(string id) => Wings.Find(w => w.Id == id);
        public int Zone(string id) => ZoneIndex.TryGetValue(id ?? "", out int i) ? i : -1;
        public ZoneDef ZoneOf(string id) { int i = Zone(id); return i >= 0 ? Zones[i] : null; }
        public TeamDef Team(string id) => TeamDefs.TryGetValue(id ?? "", out var t) ? t : null;
        public double DayMinutes => DayEnd - DayStart;
        /// <summary>Real seconds of one shift at 1x.</summary>
        public double RealSecondsPerDay => DayMinutes / Math.Max(0.01, GameMinPerRealS);
        /// <summary>Walking metres between two zones along the corridors (Floyd–Warshall once at load).</summary>
        public double Metres(int a, int b) => dist == null || a < 0 || b < 0 ? 0 : dist[a, b];
        /// <summary>Zone of a test kind (xray / lab / ct), or -1.</summary>
        public int TestZone(string test) => Zones.FindIndex(z => z.Kind == "test" && z.Test == test);
        public int ZoneOfWard(string ward) => Zones.FindIndex(z => z.Kind == "ward" && z.Ward == ward);

        public static GameData FromTexts(Func<string, string> load)
        {
            var gd = new GameData();
            gd.ReadBalance(Parse(gd, load, "balance"));
            gd.ReadMap(Parse(gd, load, "map"));
            gd.ReadResearch(Parse(gd, load, "research"));
            gd.ReadEvents(Parse(gd, load, "events"));
            gd.Validate();
            if (gd.Ok) gd.BuildDistances();
            return gd;
        }

        static object Parse(GameData gd, Func<string, string> load, string name)
        {
            string text = null;
            try { text = load(name); } catch (Exception e) { gd.Errors.Add(name + ": " + e.Message); }
            if (string.IsNullOrEmpty(text)) { gd.Errors.Add(name + ": missing"); return null; }
            var o = Json.TryParse(text);
            if (!(o is JObj)) { gd.Errors.Add(name + ": invalid json"); return null; }
            return o;
        }

        static double[] Dbl(object o, string key, double[] def, int len = -1)
        {
            var a = J.DblArr(o, key);
            if (a == null || a.Length == 0) return def;
            return len < 0 || a.Length == len ? a : def;
        }

        void ReadBalance(object b)
        {
            if (b == null) return;
            var t = J.Obj(b, "time");
            DayStart = J.I(t, "day_start", 420); DayEnd = J.I(t, "day_end", 1140); HandoverFrom = J.I(t, "handover_from", 1110);
            GameMinPerRealS = J.D(t, "game_min_per_real_s", 3); AutosaveS = J.D(t, "autosave_s", 30);
            FreeMaxSpeed = J.I(t, "free_max_speed", 2); PaidMaxSpeed = J.I(t, "paid_max_speed", 4); NightMin = J.I(t, "night_min", 720);

            var s = J.Obj(b, "start");
            StartCash = J.D(s, "cash", 1500); StartReputation = J.D(s, "reputation", 50);
            if (J.Obj(s, "teams") is JObj st) foreach (var kv in st) StartTeams[kv.Key] = J.I(kv.Value);
            if (J.Obj(s, "beds") is JObj sb) foreach (var kv in sb) StartBeds[kv.Key] = J.I(kv.Value);

            var v = J.Obj(b, "severity");
            Mix = Dbl(v, "mix", Mix, Sev.N); Pay = Dbl(v, "pay", Pay, Sev.N); Supplies = Dbl(v, "supplies", Supplies, Sev.N);
            TreatMin = Dbl(v, "treat_min", TreatMin, Sev.N); SafeWait = Dbl(v, "safe_wait", SafeWait, Sev.N); TestP = Dbl(v, "test_p", TestP, Sev.N);
            AdmitP = Dbl(v, "admit_p", AdmitP, Sev.N); StayMin = Dbl(v, "stay_min", StayMin, Sev.N); OrP = Dbl(v, "or_p", OrP, Sev.N);
            LwbsWait = Dbl(v, "lwbs_wait", LwbsWait, 2);

            var a = J.Obj(b, "arrivals");
            ArrivalsPerHour = J.D(a, "per_hour", 2.6); HourCurve = Dbl(a, "hour_curve", HourCurve); NightMult = J.D(a, "night_mult", 0.35);
            RepMin = J.D(a, "rep_min", 0.7); RepSpan = J.D(a, "rep_span", 0.6);
            var am = J.Obj(b, "ambulance");
            AmbulancePerDay = Dbl(am, "per_day", AmbulancePerDay); AmbulanceAnswerMin = J.D(am, "answer_min", 12);
            AmbulancePayMult = J.D(am, "pay_mult", 1.5); DivertRep = J.D(am, "divert_rep", 0.6); AmbulanceSev = Dbl(am, "sev", AmbulanceSev, Sev.N);

            var k = J.Obj(b, "tasks");
            RegisterMin = J.D(k, "register_min", 2); TriageMin = J.D(k, "triage_min", 4); DischargeMin = J.D(k, "discharge_min", 6);
            CleanBayMin = J.D(k, "clean_bay_min", 12); CleanBedMin = J.D(k, "clean_bed_min", 20); PorterBaseMin = J.D(k, "porter_base_min", 4);
            WalkMPerMin = J.D(k, "walk_m_per_min", 40); LiftMin = J.D(k, "lift_min", 6); OrMin = J.D(k, "or_min", 90); FastTrackMin = J.D(k, "fast_track_min", 10);
            TriageAccuracy = J.D(b, "triage_accuracy", 0.82);

            var f = J.Obj(b, "fatigue");
            FatigueBusy = J.D(f, "busy_per_min", 0.11); FatigueIdle = J.D(f, "idle_per_min", 0.05); FatigueMaxSlow = J.D(f, "max_slow", 0.35);
            RushedKeep = J.D(f, "rushed_keep", 0.5); RushedNotes = J.I(f, "rushed_notes", 2); NoteDelayMin = J.D(f, "note_delay_min", 45);
            var bo = J.Obj(b, "burnout");
            BurnoutLine = J.D(bo, "busy_line", 0.85); BurnoutRise = J.D(bo, "rise", 12); BurnoutFall = J.D(bo, "fall", 8);
            BurnoutSlowAt = J.D(bo, "slow_at", 60); BurnoutSlow = J.D(bo, "slow", 0.1);
            var n = J.Obj(b, "night");
            NightEff = J.D(n, "eff", 0.5); NightEffTeam = J.D(n, "eff_team", 0.8);

            if (J.Obj(b, "teams") is JObj tm)
                foreach (var kv in tm)
                    TeamDefs[kv.Key] = new TeamDef { Id = kv.Key, Wage = J.D(kv.Value, "wage"), Price = J.D(kv.Value, "price"), Max = J.I(kv.Value, "max", 4) };
            if (J.Obj(b, "rooms") is JObj rm)
                foreach (var kv in rm)
                    Rooms[kv.Key] = new RoomDef { Id = kv.Key, Cost = J.D(kv.Value, "cost"), ScanMin = J.D(kv.Value, "scan_min"), Upkeep = J.D(kv.Value, "upkeep") };
            if (J.Obj(b, "prices") is JObj pr) foreach (var kv in pr) Prices[kv.Key] = J.D(kv.Value);
            if (J.Obj(b, "upkeep") is JObj up) foreach (var kv in up) Upkeep[kv.Key] = J.D(kv.Value);

            var e = J.Obj(b, "economy");
            WardNight = J.D(e, "ward_night", 70); RepAlpha = J.D(e, "rep_alpha", 0.3); RepFreeDays = J.I(e, "rep_free_days", 3);
            PayRepMin = J.D(e, "pay_rep_min", 0.85); PayRepSpan = J.D(e, "pay_rep_span", 0.3); BadRep = Dbl(e, "bad_rep", BadRep, 3);
            DayIncomeFloor = J.D(e, "day_income_floor", 700);

            var sr = J.Obj(b, "stars");
            StarCare = Dbl(sr, "care", StarCare, 3); StarWaits = Dbl(sr, "waits", StarWaits, 3); StarFlow = Dbl(sr, "flow", StarFlow, 3);
            var ev = J.Obj(b, "events");
            EventFreeDays = J.I(ev, "free_days", 3); EventsPerDay = Dbl(ev, "per_day", EventsPerDay);
            var of = J.Obj(b, "offline");
            OfflineMinS = J.D(of, "min_s", 120); OfflineCapH = J.D(of, "cap_h", 4); OfflineCapHPlus = J.D(of, "cap_h_plus", 12);
            OfflineRate = J.D(of, "rate", 0.5); OfflineRatePlus = J.D(of, "rate_plus", 0.8); OfflineDaysPerHour = J.D(of, "days_per_hour", 0.5);
            if (J.Obj(b, "rewards") is JObj rw)
                foreach (var kv in rw)
                {
                    if (!(kv.Value is JObj)) { if (kv.Key == "single_max_days") RewardSingleMaxDays = J.D(kv.Value, 0.2); continue; }
                    Rewards[kv.Key] = new RewardDef
                    {
                        Id = kv.Key, PerDay = J.I(kv.Value, "per_day", 1), CooldownS = J.L(kv.Value, "cooldown_s"), Days = J.D(kv.Value, "days"),
                        Minutes = J.D(kv.Value, "minutes"), Patients = J.I(kv.Value, "patients"),
                    };
                }
        }

        void ReadMap(object m)
        {
            if (m == null) return;
            foreach (var w in J.Arr(m, "wings") ?? new List<object>())
            {
                var wd = new WingDef { Id = J.S(w, "id"), Chapter = J.I(w, "chapter", 1), Reputation = J.D(w, "reputation"), Cost = J.D(w, "cost"), Arrivals = J.D(w, "arrivals", 1) };
                if (J.Obj(w, "start_beds") is JObj sb) foreach (var kv in sb) wd.StartBeds[kv.Key] = J.I(kv.Value);
                Wings.Add(wd);
            }
            foreach (var z in J.Arr(m, "zones") ?? new List<object>())
            {
                var zd = new ZoneDef
                {
                    Index = Zones.Count, Id = J.S(z, "id"), Kind = J.S(z, "kind"), Wing = J.S(z, "wing"), Test = J.S(z, "test"), Ward = J.S(z, "ward"),
                    X = J.D(z, "x"), Y = J.D(z, "y"), W = J.D(z, "w", 6), D = J.D(z, "d", 6), Slots = J.I(z, "slots"),
                };
                if (ZoneIndex.ContainsKey(zd.Id)) { Errors.Add("map: duplicate zone " + zd.Id); continue; }
                ZoneIndex[zd.Id] = zd.Index;
                Zones.Add(zd);
            }
            foreach (var c in J.Arr(m, "corridors") ?? new List<object>())
            {
                if (!(c is List<object> l) || l.Count < 2) { Errors.Add("map: bad corridor"); continue; }
                int a = Zone(J.Str(l[0])), b = Zone(J.Str(l[1]));
                if (a < 0 || b < 0) { Errors.Add("map: corridor to unknown zone " + J.Str(l[0]) + "-" + J.Str(l[1])); continue; }
                Corridors.Add(new[] { a, b });
            }
        }

        void ReadResearch(object r)
        {
            if (r == null) return;
            foreach (var n in J.Arr(r, "nodes") ?? new List<object>())
            {
                var d = new ResearchDef { Id = J.S(n, "id"), Branch = J.S(n, "branch"), Cost = J.D(n, "cost"), Minutes = J.D(n, "min"), Wing = J.S(n, "wing"), Requires = J.StrList(n, "requires") ?? new List<string>() };
                Research.Add(d);
                ResearchById[d.Id] = d;
            }
        }

        void ReadEvents(object r)
        {
            if (r == null) return;
            foreach (var n in J.Arr(r, "events") ?? new List<object>())
                Events.Add(new EventDef
                {
                    Id = J.S(n, "id"), Weight = J.D(n, "weight", 1), MinDay = J.I(n, "min_day", 1), Days = J.I(n, "days", 1), Mult = J.D(n, "mult", 1),
                    Min = J.I(n, "min"), Max = J.I(n, "max"), Minutes = J.I(n, "minutes", 60), Beds = J.I(n, "beds", 2), Announced = J.B(n, "announced"),
                    Bonus = J.D(n, "bonus"),
                });
        }

        void Validate()
        {
            if (Wings.Count == 0) Errors.Add("map: no wings");
            foreach (var z in Zones) if (Wing(z.Wing) == null) Errors.Add("map: zone " + z.Id + " unknown wing");
            foreach (var w in Wings) foreach (var kv in w.StartBeds) if (Zone(kv.Key) < 0 || Zones[Zone(kv.Key)].Wing != w.Id) Errors.Add("map: wing " + w.Id + " start bed zone " + kv.Key);
            foreach (var kv in StartBeds) if (Zone(kv.Key) < 0) Errors.Add("balance: start bed zone " + kv.Key);
            foreach (var kind in new[] { "reception", "waiting", "triage", "bays", "ambulance" })
                if (!Zones.Exists(z => z.Kind == kind)) Errors.Add("map: no " + kind + " zone");
            foreach (var w in new[] { "obs", "general", "surgical", "icu" }) if (ZoneOfWard(w) < 0) Errors.Add("map: no ward " + w);
            foreach (var t in new[] { "xray", "lab", "ct" })
            {
                if (TestZone(t) < 0) Errors.Add("map: no test room " + t);
                if (!Rooms.ContainsKey(t)) Errors.Add("balance: room " + t);
            }
            if (!Rooms.ContainsKey("or")) Errors.Add("balance: room or");
            foreach (var id in Teams.All) if (!TeamDefs.ContainsKey(id)) Errors.Add("balance: team " + id);
            if (HourCurve.Length * 60 < DayMinutes) Errors.Add("balance: hour_curve shorter than the shift");
            if (HandoverFrom <= DayStart || HandoverFrom >= DayEnd) Errors.Add("balance: handover window");
            foreach (var r in Research)
            {
                foreach (var q in r.Requires) if (!ResearchById.ContainsKey(q)) Errors.Add("research: " + r.Id + " requires unknown " + q);
                if (r.Minutes > DayMinutes) Errors.Add("research: " + r.Id + " longer than one shift");
                if (r.Minutes / GameMinPerRealS > 20 * 60) Errors.Add("research: " + r.Id + " longer than 20 real minutes");
                if (!string.IsNullOrEmpty(r.Wing) && Wing(r.Wing) == null) Errors.Add("research: " + r.Id + " wing");
            }
            foreach (var id in new[] { "offline_x2", "float_nurse", "call_neighbor", "daily_bonus" })
                if (!Rewards.ContainsKey(id)) Errors.Add("balance: reward " + id);
        }

        void BuildDistances()
        {
            int n = Zones.Count;
            dist = new double[n, n];
            for (int i = 0; i < n; i++) for (int j = 0; j < n; j++) dist[i, j] = i == j ? 0 : 1e9;
            foreach (var c in Corridors)
            {
                double dx = Zones[c[0]].X - Zones[c[1]].X, dy = Zones[c[0]].Y - Zones[c[1]].Y;
                double d = Math.Sqrt(dx * dx + dy * dy);
                dist[c[0], c[1]] = Math.Min(dist[c[0], c[1]], d);
                dist[c[1], c[0]] = Math.Min(dist[c[1], c[0]], d);
            }
            for (int k = 0; k < n; k++)
                for (int i = 0; i < n; i++)
                    for (int j = 0; j < n; j++)
                        if (dist[i, k] + dist[k, j] < dist[i, j]) dist[i, j] = dist[i, k] + dist[k, j];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                    if (dist[i, j] >= 1e9) Errors.Add("map: zone " + Zones[j].Id + " unreachable from " + Zones[i].Id);
        }
    }
}
