// Ridgeback Rangers sim core — engine independent (no UnityEngine). C# 9 / .NET Standard 2.1.
// Typed view of the data files in Assets/Resources/data (balance.json, map.json, species.json, events.json).
// The Unity layer passes a loader (name -> text); tests read the files from disk. Errors are collected, never thrown.
using System;
using System.Collections.Generic;

namespace RidgebackRangers.Sim
{
    public static class Roles
    {
        public const string Feeder = "feeder", Vet = "vet", Handler = "handler", Tech = "tech";
        public static readonly string[] All = { Feeder, Vet, Handler, Tech };
        public static bool Valid(string r) => Array.IndexOf(All, r) >= 0;
    }

    public static class PinKinds
    {
        public const string Hungry = "hungry", Squabble = "squabble", Sick = "sick", Fence = "fence", Escape = "escape", Flood = "flood";
        public static readonly string[] All = { Hungry, Squabble, Sick, Fence, Escape, Flood };
        public static bool Valid(string k) => Array.IndexOf(All, k) >= 0;
    }

    public sealed class ZoneDef { public string Id, Biome; public int Chapter; public double Rating, Cost; }
    public sealed class NodeDef { public int Index; public string Id, Kind, Zone; public double X, Y; }
    public sealed class EdgeDef { public int A, B; public double Km; public bool Alley; }
    public sealed class DigSiteDef { public string Id, Zone; public int Node; public List<string> Species = new List<string>(); }
    public sealed class SpeciesDef
    {
        public string Id, Family, Biome, Diet, Temperament, Tint;
        public int Space, Fence;
        public double Appeal, Crates, HungerH, HatchMin, Scale;
    }
    public sealed class RoleDef { public string Id; public double Wage, WorkMin; }
    public sealed class PinDef { public string Id, Role; public double Deadline, Fine; }
    public sealed class EventDef { public string Id, Zone; public double Weight, Mult; public int MinDay, Days, From, Min, Max, BusyMin; }
    public sealed class RewardDef { public string Id; public int PerDay; public long CooldownS; public double Days; }

    public sealed class GameData
    {
        public readonly List<string> Errors = new List<string>();
        public bool Ok => Errors.Count == 0;

        // time
        public int DayStart = 480, DayEnd = 1080, FreeMaxSpeed = 2, PaidMaxSpeed = 3, GateCloseBeforeEnd = 40;
        public double GameMinPerRealS = 2.2, AutosaveS = 30;
        // start
        public double StartCash, StartCp, StartRating, StartHunger = 0.45;
        public int StartFeedPlan = 8, StartIncubators = 2, StartTramCars = 1;
        public List<string> StartZones = new List<string>();
        public readonly Dictionary<string, int> StartRangers = new Dictionary<string, int>();
        public readonly List<KeyValuePair<string, List<string>>> StartPaddocks = new List<KeyValuePair<string, List<string>>>();
        // rangers + pins
        public readonly Dictionary<string, RoleDef> RoleDefs = new Dictionary<string, RoleDef>();
        public double RangerKmh = 9, LaneMult = 1.3, VetBedsWorkMin = 12, ChiefDelayMin = 6;
        public int RangerCap = 4, RangerCapPass = 5, FeederCarry = 6;
        public readonly Dictionary<string, PinDef> Pins = new Dictionary<string, PinDef>();
        public double SickPerH = 0.0025, SickStressMult = 3;
        public double[] SickSeverity = { 0.6, 0.3, 0.1 }, SickDeadline = { 240, 150, 60 };
        public double SquabblePerH = 0.3, SquabbleCrowd = 0.25;
        public double FenceWearH = 0.016, FenceBossyMult = 1, FencePinBelow = 0.6, EscapeChance = 0.6;
        // feeding + mood + appeal
        public double EatAt = 0.35, EatGain = 0.6, HungryPinAt = 0.7, CrateCost = 10, SpoilH = 0.1, EatShowMin = 25, Midday = 780;
        public int StationCap = 12, StationCapPlus = 8;
        public double MoodRateH = 0.12, MoodBase = 0.45, MoodHarmony = 0.35, MoodFed = 0.25, MoodCrowd = 0.2, MoodBiome = 0.25, MoodHeat = 0.2, MoodWater = 0.15;
        public int YoungDays = 2;
        public double AppealYoung = 0.6, AppealSchoolYoung = 1.5, AppealSick = 0.5, AppealShow = 1.3;
        public double[] Variety = { 0.8, 1.0, 1.15, 1.25 };
        // harmony
        public readonly Dictionary<string, double> Temperament = new Dictionary<string, double>();
        public double HarmonySame = 0.15, HarmonyOtherDiet = 0.08, HarmonyBiome = -0.25;
        // visitors + tram
        public double VisitorsPerH = 26, RatingMin = 0.6, RatingSpan = 0.8, AppealBase = 0.5, AppealDiv = 40, AppealMax = 2.5;
        public double[] HourCurve = { 1 };
        public double Ticket = 5, TicketRatingMin = 0.85, TicketRatingSpan = 0.3, Patience = 40, Snack = 1.2, CafePerLevel = 0.35, Gift = 1.5,
            Donation = 0.9, CpPerJoy = 0.002, JoyTarget = 32, WalkJoy = 0.4, WalkSat = 0.25;
        public double TramSeats = 16, TramKmh = 14, TramDwell = 3, TramMaxWait = 12, TramUpkeep = 40, TramUpkeepCar = 30;
        public int TramCarsMax = 4;
        // economy
        public double PaddockUpkeep = 25, RatingAlpha = 0.3, CpPerCare = 30, DayIncomeFloor = 1000;
        public int RatingFreeDays = 3;
        public double[] StarCare = { 0.55, 0.7, 0.82 }, StarVisitors = { 0.45, 0.6, 0.75 }, StarCalm = { 0.5, 0.75, 0.9 };
        public int PaddockSlots = 6, ExpandSlots = 3, ExpandMax = 2;
        public double DigCp = 15, DigWorkMin = 100, DigShards = 35;
        public int IncubatorMax = 6;
        public double BandLo = 40, BandHi = 60, TutorialLo = 10, TutorialHi = 90, WarmS = 600;
        public readonly Dictionary<string, double> Prices = new Dictionary<string, double>();
        public double ChiefRating = 55;
        public int[] CallsPerDay = { 2, 3, 3, 4 };
        public List<string> CallKinds = new List<string>();
        public int EventFreeDays = 3;
        public double[] EventsPerDay = { 0, 0, 0.6, 1.0 };
        public double OfflineMinS = 120, OfflineCapH = 4, OfflineCapHPlus = 10, OfflineRate = 0.5, OfflineRatePlus = 0.75, OfflineDaysPerHour = 0.5;
        public readonly Dictionary<string, RewardDef> Rewards = new Dictionary<string, RewardDef>();
        public double RewardSingleMaxDays = 0.2;

        // map
        public double RoadFactor = 1.15;
        public readonly List<ZoneDef> Zones = new List<ZoneDef>();
        public readonly List<NodeDef> Nodes = new List<NodeDef>();
        public readonly List<EdgeDef> Edges = new List<EdgeDef>();
        public readonly Dictionary<string, int> NodeIndex = new Dictionary<string, int>();
        public readonly List<DigSiteDef> DigSites = new List<DigSiteDef>();
        public int HqNode = -1, GateNode = -1, BarnNode = -1, StationNode = -1, VetNode = -1, HatcheryNode = -1;
        public RoadGraph Graph;

        public readonly List<SpeciesDef> Species = new List<SpeciesDef>();
        public readonly Dictionary<string, SpeciesDef> SpeciesById = new Dictionary<string, SpeciesDef>();
        public readonly List<EventDef> Events = new List<EventDef>();

        public double Price(string id) => Prices.TryGetValue(id, out var p) ? p : 0;
        public ZoneDef Zone(string id) => Zones.Find(z => z.Id == id);
        public int Node(string id) => NodeIndex.TryGetValue(id ?? "", out int i) ? i : -1;
        public SpeciesDef Sp(string id) => SpeciesById.TryGetValue(id ?? "", out var s) ? s : null;
        public double DayMinutes => DayEnd - DayStart;
        /// <summary>Real seconds of one park day at 1x.</summary>
        public double RealSecondsPerDay => DayMinutes / Math.Max(0.01, GameMinPerRealS);
        /// <summary>The zone's viewpoint node (tram stop + feeding station).</summary>
        public int ViewOf(string zone) => Nodes.FindIndex(n => n.Zone == zone && n.Kind == "view");

        public double Harmony(string tempA, string tempB)
        {
            string a = string.CompareOrdinal(tempA, tempB) <= 0 ? tempA : tempB, b = a == tempA ? tempB : tempA;
            return Temperament.TryGetValue(a + "-" + b, out var h) ? h : Temperament.TryGetValue(b + "-" + a, out h) ? h : 0.5;
        }

        public static GameData FromTexts(Func<string, string> load)
        {
            var gd = new GameData();
            gd.ReadBalance(Parse(gd, load, "balance"));
            gd.ReadMap(Parse(gd, load, "map"));
            gd.ReadSpecies(Parse(gd, load, "species"));
            gd.ReadEvents(Parse(gd, load, "events"));
            gd.Validate();
            if (gd.Ok) gd.Graph = new RoadGraph(gd);
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

        static double[] Dbl(object o, string key, double[] def)
        {
            var a = J.DblArr(o, key);
            return a != null && a.Length > 0 ? a : def;
        }

        static int[] Ints(object o, string key, int[] def)
        {
            var a = J.DblArr(o, key);
            if (a == null || a.Length == 0) return def;
            var r = new int[a.Length];
            for (int i = 0; i < a.Length; i++) r[i] = (int)Math.Round(a[i]);
            return r;
        }

        void ReadBalance(object b)
        {
            if (b == null) return;
            var t = J.Obj(b, "time");
            DayStart = J.I(t, "day_start", 480); DayEnd = J.I(t, "day_end", 1080);
            GameMinPerRealS = J.D(t, "game_min_per_real_s", 2.2); AutosaveS = J.D(t, "autosave_s", 30);
            FreeMaxSpeed = J.I(t, "free_max_speed", 2); PaidMaxSpeed = J.I(t, "paid_max_speed", 3);
            GateCloseBeforeEnd = J.I(t, "gate_close_before_end", 40);

            var s = J.Obj(b, "start");
            StartCash = J.D(s, "cash", 1500); StartCp = J.D(s, "cp", 20); StartRating = J.D(s, "rating", 50);
            StartZones = J.StrList(s, "zones") ?? new List<string>();
            StartHunger = J.D(s, "hunger", 0.45); StartFeedPlan = J.I(s, "feed_plan", 8);
            StartIncubators = J.I(s, "incubators", 2); StartTramCars = J.I(s, "tram_cars", 1);
            if (J.Obj(s, "rangers") is JObj rr) foreach (var kv in rr) StartRangers[kv.Key] = J.I(kv.Value);
            foreach (var p in J.Arr(s, "paddocks") ?? new List<object>())
                StartPaddocks.Add(new KeyValuePair<string, List<string>>(J.S(p, "node"), J.StrList(p, "animals") ?? new List<string>()));

            if (J.Obj(b, "roles") is JObj ro)
                foreach (var kv in ro) RoleDefs[kv.Key] = new RoleDef { Id = kv.Key, Wage = J.D(kv.Value, "wage"), WorkMin = J.D(kv.Value, "work_min", 10) };
            var rg = J.Obj(b, "rangers");
            RangerKmh = J.D(rg, "speed_kmh", 9); LaneMult = J.D(rg, "lane_mult", 1.3); RangerCap = J.I(rg, "cap", 4); RangerCapPass = J.I(rg, "cap_pass", 5);
            FeederCarry = J.I(rg, "feeder_carry", 6); VetBedsWorkMin = J.D(rg, "vet_beds_work_min", 12); ChiefDelayMin = J.D(rg, "chief_delay_min", 6);
            if (J.Obj(b, "pins") is JObj pi)
                foreach (var kv in pi) Pins[kv.Key] = new PinDef { Id = kv.Key, Role = J.S(kv.Value, "role"), Deadline = J.D(kv.Value, "deadline", 90), Fine = J.D(kv.Value, "fine") };
            var sk = J.Obj(b, "sick");
            SickPerH = J.D(sk, "per_h", 0.0025); SickStressMult = J.D(sk, "stress_mult", 3);
            SickSeverity = Dbl(sk, "severity", SickSeverity); SickDeadline = Dbl(sk, "deadline", SickDeadline);
            var sq = J.Obj(b, "squabble");
            SquabblePerH = J.D(sq, "per_h", 0.3); SquabbleCrowd = J.D(sq, "crowd_per_slot", 0.25);
            var fe = J.Obj(b, "fence");
            FenceWearH = J.D(fe, "wear_h", 0.016); FenceBossyMult = J.D(fe, "bossy_mult", 1); FencePinBelow = J.D(fe, "pin_below", 0.6); EscapeChance = J.D(fe, "escape_chance", 0.6);
            var fd = J.Obj(b, "feeding");
            EatAt = J.D(fd, "eat_at", 0.35); EatGain = J.D(fd, "eat_gain", 0.6); HungryPinAt = J.D(fd, "hungry_pin_at", 0.7); CrateCost = J.D(fd, "crate_cost", 10);
            StationCap = J.I(fd, "station_cap", 12); StationCapPlus = J.I(fd, "station_cap_plus", 8); SpoilH = J.D(fd, "spoil_h", 0.1);
            EatShowMin = J.D(fd, "eat_show_min", 25); Midday = J.D(fd, "midday", 780);
            var mo = J.Obj(b, "mood");
            MoodRateH = J.D(mo, "rate_h", 0.12); MoodBase = J.D(mo, "base", 0.45); MoodHarmony = J.D(mo, "harmony", 0.35); MoodFed = J.D(mo, "fed", 0.25);
            MoodCrowd = J.D(mo, "crowd", 0.2); MoodBiome = J.D(mo, "biome", 0.25); MoodHeat = J.D(mo, "heat", 0.2); MoodWater = J.D(mo, "water", 0.15);
            YoungDays = J.I(mo, "young_days", 2);
            var ap = J.Obj(b, "appeal");
            AppealYoung = J.D(ap, "young", 0.6); AppealSchoolYoung = J.D(ap, "school_young", 1.5); AppealSick = J.D(ap, "sick", 0.5); AppealShow = J.D(ap, "show", 1.3);
            Variety = Dbl(ap, "variety", Variety);
            var vi = J.Obj(b, "visitors");
            VisitorsPerH = J.D(vi, "base_per_h", 26); HourCurve = Dbl(vi, "hour_curve", HourCurve);
            RatingMin = J.D(vi, "rating_min", 0.6); RatingSpan = J.D(vi, "rating_span", 0.8);
            AppealBase = J.D(vi, "appeal_base", 0.5); AppealDiv = J.D(vi, "appeal_div", 40); AppealMax = J.D(vi, "appeal_max", 2.5);
            Ticket = J.D(vi, "ticket", 5); TicketRatingMin = J.D(vi, "ticket_rating_min", 0.85); TicketRatingSpan = J.D(vi, "ticket_rating_span", 0.3);
            Patience = J.D(vi, "patience", 40); Snack = J.D(vi, "snack", 1.2); CafePerLevel = J.D(vi, "cafe_per_level", 0.35); Gift = J.D(vi, "gift", 1.5);
            Donation = J.D(vi, "donation", 0.9); CpPerJoy = J.D(vi, "cp_per_joy", 0.002); JoyTarget = J.D(vi, "joy_target", 32);
            WalkJoy = J.D(vi, "walk_joy", 0.4); WalkSat = J.D(vi, "walk_sat", 0.25);
            var tr = J.Obj(b, "tram");
            TramSeats = J.D(tr, "seats", 16); TramKmh = J.D(tr, "speed_kmh", 14); TramDwell = J.D(tr, "dwell_min", 3); TramMaxWait = J.D(tr, "max_wait", 12);
            TramCarsMax = J.I(tr, "cars_max", 4); TramUpkeep = J.D(tr, "upkeep", 40); TramUpkeepCar = J.D(tr, "upkeep_car", 30);
            var ec = J.Obj(b, "economy");
            PaddockUpkeep = J.D(ec, "paddock_upkeep", 25); RatingAlpha = J.D(ec, "rating_alpha", 0.3); RatingFreeDays = J.I(ec, "rating_free_days", 3);
            CpPerCare = J.D(ec, "cp_per_care", 30); DayIncomeFloor = J.D(ec, "day_income_floor", 1000);
            var st = J.Obj(b, "stars");
            StarCare = Dbl(st, "care", StarCare); StarVisitors = Dbl(st, "visitors", StarVisitors); StarCalm = Dbl(st, "calm", StarCalm);
            var pd = J.Obj(b, "paddock");
            PaddockSlots = J.I(pd, "slots", 6); ExpandSlots = J.I(pd, "expand_slots", 3); ExpandMax = J.I(pd, "expand_max", 2);
            var dg = J.Obj(b, "dig");
            DigCp = J.D(dg, "cp", 15); DigWorkMin = J.D(dg, "work_min", 100); DigShards = J.D(dg, "shards", 35);
            var inc = J.Obj(b, "incubator");
            IncubatorMax = J.I(inc, "max", 6); BandLo = J.D(inc, "band_lo", 40); BandHi = J.D(inc, "band_hi", 60);
            TutorialLo = J.D(inc, "tutorial_lo", 10); TutorialHi = J.D(inc, "tutorial_hi", 90); WarmS = J.D(inc, "warm_s", 600);
            if (J.Obj(b, "prices") is JObj pr) foreach (var kv in pr) Prices[kv.Key] = J.D(kv.Value);
            ChiefRating = J.D(J.Obj(b, "chief"), "rating", 55);
            var ca = J.Obj(b, "calls");
            CallsPerDay = Ints(ca, "per_day", CallsPerDay); CallKinds = J.StrList(ca, "kinds") ?? new List<string> { "fence", "sick" };
            var ev = J.Obj(b, "events");
            EventFreeDays = J.I(ev, "free_days", 3); EventsPerDay = Dbl(ev, "per_day", EventsPerDay);
            var of = J.Obj(b, "offline");
            OfflineMinS = J.D(of, "min_s", 120); OfflineCapH = J.D(of, "cap_h", 4); OfflineCapHPlus = J.D(of, "cap_h_plus", 10);
            OfflineRate = J.D(of, "rate", 0.5); OfflineRatePlus = J.D(of, "rate_plus", 0.75); OfflineDaysPerHour = J.D(of, "days_per_hour", 0.5);
            if (J.Obj(b, "rewards") is JObj rw)
                foreach (var kv in rw)
                {
                    if (!(kv.Value is JObj)) { if (kv.Key == "single_max_days") RewardSingleMaxDays = J.D(kv.Value, 0.2); continue; }
                    Rewards[kv.Key] = new RewardDef { Id = kv.Key, PerDay = J.I(kv.Value, "per_day", 1), CooldownS = J.L(kv.Value, "cooldown_s"), Days = J.D(kv.Value, "days") };
                }
        }

        void ReadMap(object m)
        {
            if (m == null) return;
            RoadFactor = J.D(m, "road_factor", 1.15);
            foreach (var z in J.Arr(m, "zones") ?? new List<object>())
                Zones.Add(new ZoneDef { Id = J.S(z, "id"), Chapter = J.I(z, "chapter"), Biome = J.S(z, "biome", "forest"), Rating = J.D(z, "rating"), Cost = J.D(z, "cost") });
            foreach (var n in J.Arr(m, "nodes") ?? new List<object>())
            {
                var nd = new NodeDef { Index = Nodes.Count, Id = J.S(n, "id"), X = J.D(n, "x"), Y = J.D(n, "y"), Kind = J.S(n, "kind"), Zone = J.S(n, "zone") };
                if (NodeIndex.ContainsKey(nd.Id)) { Errors.Add("map: duplicate node " + nd.Id); continue; }
                NodeIndex[nd.Id] = nd.Index;
                Nodes.Add(nd);
                switch (nd.Kind)
                {
                    case "hq": HqNode = nd.Index; break;
                    case "gate": GateNode = nd.Index; break;
                    case "barn": BarnNode = nd.Index; break;
                    case "station": StationNode = nd.Index; break;
                    case "vet": VetNode = nd.Index; break;
                    case "hatchery": HatcheryNode = nd.Index; break;
                }
            }
            foreach (var e in J.Arr(m, "edges") ?? new List<object>())
            {
                if (!(e is List<object> l) || l.Count < 2) { Errors.Add("map: bad edge"); continue; }
                int a = Node(J.Str(l[0])), b = Node(J.Str(l[1]));
                if (a < 0 || b < 0) { Errors.Add("map: edge to unknown node " + J.Str(l[0]) + "-" + J.Str(l[1])); continue; }
                double dx = Nodes[a].X - Nodes[b].X, dy = Nodes[a].Y - Nodes[b].Y;
                Edges.Add(new EdgeDef { A = a, B = b, Km = Math.Sqrt(dx * dx + dy * dy) * RoadFactor });
            }
            foreach (var d in J.Arr(m, "dig_sites") ?? new List<object>())
                DigSites.Add(new DigSiteDef { Id = J.S(d, "id"), Zone = J.S(d, "zone"), Node = Node(J.S(d, "id")), Species = J.StrList(d, "species") ?? new List<string>() });
        }

        void ReadSpecies(object r)
        {
            if (r == null) return;
            if (J.Obj(r, "temperament") is JObj tm) foreach (var kv in tm) Temperament[kv.Key] = J.D(kv.Value);
            var h = J.Obj(r, "harmony");
            HarmonySame = J.D(h, "same_species", 0.15); HarmonyOtherDiet = J.D(h, "other_diet", 0.08); HarmonyBiome = J.D(h, "biome_mismatch", -0.25);
            foreach (var n in J.Arr(r, "species") ?? new List<object>())
            {
                var s = new SpeciesDef
                {
                    Id = J.S(n, "id"), Family = J.S(n, "family"), Biome = J.S(n, "biome"), Diet = J.S(n, "diet"), Temperament = J.S(n, "temperament"),
                    Tint = J.S(n, "tint", "#FFFFFF"), Space = Math.Max(1, J.I(n, "space", 1)), Fence = Math.Max(1, J.I(n, "fence", 1)),
                    Appeal = J.D(n, "appeal", 8), Crates = J.D(n, "crates", 1), HungerH = J.D(n, "hunger_h", 0.08), HatchMin = J.D(n, "hatch_min", 8), Scale = J.D(n, "scale", 1),
                };
                if (SpeciesById.ContainsKey(s.Id)) { Errors.Add("species: duplicate " + s.Id); continue; }
                Species.Add(s);
                SpeciesById[s.Id] = s;
            }
        }

        void ReadEvents(object r)
        {
            if (r == null) return;
            foreach (var n in J.Arr(r, "events") ?? new List<object>())
                Events.Add(new EventDef
                {
                    Id = J.S(n, "id"), Weight = J.D(n, "weight", 1), MinDay = J.I(n, "min_day", 1), Zone = J.S(n, "zone"), Days = J.I(n, "days", 1),
                    Mult = J.D(n, "mult", 1), From = J.I(n, "from"), Min = J.I(n, "min", 1), Max = J.I(n, "max", 1), BusyMin = J.I(n, "busy_min", 120),
                });
        }

        void Validate()
        {
            foreach (var (k, name) in new[] { (HqNode, "hq"), (GateNode, "gate"), (BarnNode, "barn"), (StationNode, "station"), (VetNode, "vet"), (HatcheryNode, "hatchery") })
                if (k < 0) Errors.Add("map: no " + name);
            foreach (var n in Nodes) if (Zone(n.Zone) == null) Errors.Add("map: node " + n.Id + " unknown zone");
            foreach (var z in Zones) if (ViewOf(z.Id) < 0) Errors.Add("map: zone " + z.Id + " has no view");
            foreach (var r in Roles.All) if (!RoleDefs.ContainsKey(r)) Errors.Add("balance: role " + r);
            foreach (var k in PinKinds.All)
            {
                if (!Pins.ContainsKey(k)) Errors.Add("balance: pin " + k);
                else if (!Roles.Valid(Pins[k].Role)) Errors.Add("balance: pin " + k + " role");
            }
            foreach (var z in StartZones) if (Zone(z) == null) Errors.Add("balance: start zone " + z);
            foreach (var kv in StartPaddocks)
            {
                int n = Node(kv.Key);
                if (n < 0 || Nodes[n].Kind != "paddock") Errors.Add("balance: start paddock " + kv.Key);
                foreach (var a in kv.Value) if (Sp(a) == null) Errors.Add("balance: start animal " + a);
            }
            foreach (var d in DigSites)
            {
                if (d.Node < 0 || Nodes[d.Node].Kind != "dig") Errors.Add("map: dig site node " + d.Id);
                foreach (var s in d.Species) if (Sp(s) == null) Errors.Add("map: dig site " + d.Id + " species " + s);
            }
            foreach (var s in Species)
            {
                if (s.HatchMin > 20) Errors.Add("species: " + s.Id + " hatch longer than 20 min");
                if (s.Biome != "forest" && s.Biome != "dune") Errors.Add("species: " + s.Id + " biome");
            }
            if (Species.Count == 0) Errors.Add("species: none");
            foreach (var id in new[] { "dawn_double", "borrow_ranger", "warm_incubator", "storm_shelter", "scout_tip" })
                if (!Rewards.ContainsKey(id)) Errors.Add("balance: reward " + id);
            if (HourCurve.Length < 1) Errors.Add("balance: hour curve");
        }
    }
}
