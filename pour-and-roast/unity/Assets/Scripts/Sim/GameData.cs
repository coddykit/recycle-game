// Pour & Roast sim core — engine independent (no UnityEngine). C# 9 / .NET Standard 2.1.
// Typed view of the data files in Assets/Resources/data (balance.json, map.json, research.json, events.json).
// The Unity layer passes a loader (name -> text); tests read the files from disk. Errors are collected, never thrown.
using System;
using System.Collections.Generic;

namespace PourAndRoast.Sim
{
    public static class Mats
    {
        public const int Pet = 0, Alu = 1, Glass = 2, N = 3;
        public static readonly string[] Ids = { "pet", "alu", "glass" };
        public static int Index(string id) => Array.IndexOf(Ids, id);
    }

    public sealed class MaterialDef { public string Id; public double VolL, Kg, BaleKg, BalePrice; }
    public sealed class ArchetypeDef { public string Id; public int Min, Max; public double[] Mix; public double Weekend, Evening; public double Mean => (Min + Max) * 0.5; }
    public sealed class PointKindDef { public string Id; public double CapL, LeaseDay, JamPer1000, ShopFee, PickupBonus; public bool Citizens; public double[] Mix; }
    public sealed class VehicleDef { public string Id; public double VolL, Kg, SpeedKmh, CostKm, ServiceMin, PetCompress, Price; public bool Alley; public string Research; }
    public sealed class DistrictDef { public string Id; public int Chapter; public double Rating, Cost; public double[] Archetypes; }
    public sealed class NodeDef { public int Index; public string Id, District, Kind, Site; public double X, Y, Rate; public bool Alley, EventSite; }
    public sealed class EdgeDef { public int A, B; public double Km; public bool Alley; }
    public sealed class ResearchDef { public string Id, Branch, District; public double Cost, Minutes; public List<string> Requires = new List<string>(); }
    public sealed class EventDef { public string Id, District; public double Weight, Mult; public int MinDay, Days, From, To, BusyMin, Min, Max, BrokenMin; }
    public sealed class RewardDef { public string Id; public int PerDay; public long CooldownS; public double Days; }

    public sealed class GameData
    {
        public readonly List<string> Errors = new List<string>();
        public bool Ok => Errors.Count == 0;

        // time
        public int DayStart = 540, DayEnd = 1140, FreeMaxSpeed = 2, PaidMaxSpeed = 4, DepotWrapupMin = 60;
        public double GameMinPerRealS = 2.5, AutosaveS = 30;
        // start
        public double StartCash, StartRating, StartBalerPetKg;
        public int StartDrivers, StartTechs;
        public List<string> StartTrucks = new List<string>();
        public List<string> StartMachines = new List<string>();
        public Dictionary<string, double> StartFill = new Dictionary<string, double>();

        public readonly MaterialDef[] Materials = new MaterialDef[Mats.N];
        public readonly List<ArchetypeDef> Archetypes = new List<ArchetypeDef>();
        public double[] HourCurve = { 1 };
        public double WeekendMult = 1.25;
        public readonly Dictionary<string, PointKindDef> PointKinds = new Dictionary<string, PointKindDef>();
        public double CompactorPet = 2.5, CompactorJamMult = 1.6;
        public readonly Dictionary<string, VehicleDef> Vehicles = new Dictionary<string, VehicleDef>();
        public readonly List<string> VehicleOrder = new List<string>();
        public double UnloadBaseMin = 8, UnloadPerContainerMin = 0.0025, BreakdownPerKm = 0.0015, GlassBreakPerKm = 0.0006;
        // economy
        public double Fee = 1, BulkFee = 0.5, FeeRatingMin = 0.85, FeeRatingSpan = 0.3, DriverWage = 110, TechWage = 90,
            DepotPower = 35, LinePower = 15, RatingAlpha = 0.3, DayIncomeFloor = 600;
        public int TurnedRatingFreeDays = 3;
        // depot
        public double CountPerHour = 420, BalerKgPerHour = 90, BalerJamPerBale = 0.02, GlassBayT = 3, TechSpeedKmh = 30, TechFixMin = 20;
        public int LinesStart = 1, LinesMax = 3, YardCap = 12, IntakeCap = 2500;
        public readonly Dictionary<string, double> Prices = new Dictionary<string, double>();
        // dispatcher
        public int DispatcherSlots = 3, PassSlots = 1, PlusSlots = 3, DispatchEveryMin = 15, MaxStops = 8;
        // stars
        public double[] StarService = { 0.12, 0.06, 0.02 }, StarEfficiency = { 12, 25, 40 }, StarThroughput = { 0.7, 0.35, 0.1 };
        // events
        public int EventFreeDays = 3;
        public double[] EventsPerDay = { 0, 0.6, 1, 1.4, 1.7 };
        // offline
        public double OfflineMinS = 120, OfflineCapH = 4, OfflineCapHPlus = 10, OfflineRate = 0.5, OfflineRatePlus = 0.75, OfflineDaysPerHour = 0.5;
        public readonly Dictionary<string, RewardDef> Rewards = new Dictionary<string, RewardDef>();
        public double RewardSingleMaxDays = 0.2;

        // map
        public double RoadFactor = 1.2;
        public readonly List<DistrictDef> Districts = new List<DistrictDef>();
        public readonly List<NodeDef> Nodes = new List<NodeDef>();
        public readonly List<EdgeDef> Edges = new List<EdgeDef>();
        public readonly Dictionary<string, int> NodeIndex = new Dictionary<string, int>();
        public int DepotNode = -1;
        public RoadGraph Graph;

        public readonly List<ResearchDef> Research = new List<ResearchDef>();
        public readonly Dictionary<string, ResearchDef> ResearchById = new Dictionary<string, ResearchDef>();
        public readonly List<EventDef> Events = new List<EventDef>();

        public double Price(string id) => Prices.TryGetValue(id, out var p) ? p : 0;
        public DistrictDef District(string id) => Districts.Find(d => d.Id == id);
        public int Node(string id) => NodeIndex.TryGetValue(id ?? "", out int i) ? i : -1;
        public double DayMinutes => DayEnd - DayStart;
        /// <summary>Real seconds of one work day at 1x.</summary>
        public double RealSecondsPerDay => DayMinutes / Math.Max(0.01, GameMinPerRealS);

        public static GameData FromTexts(Func<string, string> load)
        {
            var gd = new GameData();
            gd.ReadBalance(Parse(gd, load, "balance"));
            gd.ReadMap(Parse(gd, load, "map"));
            gd.ReadResearch(Parse(gd, load, "research"));
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

        void ReadBalance(object b)
        {
            if (b == null) return;
            var t = J.Obj(b, "time");
            DayStart = J.I(t, "day_start", 540); DayEnd = J.I(t, "day_end", 1140);
            GameMinPerRealS = J.D(t, "game_min_per_real_s", 2.5); AutosaveS = J.D(t, "autosave_s", 30);
            FreeMaxSpeed = J.I(t, "free_max_speed", 2); PaidMaxSpeed = J.I(t, "paid_max_speed", 4);
            DepotWrapupMin = J.I(t, "depot_wrapup_min", 60);

            var s = J.Obj(b, "start");
            StartCash = J.D(s, "cash", 2000); StartRating = J.D(s, "rating", 50);
            StartDrivers = J.I(s, "drivers", 1); StartTechs = J.I(s, "techs", 0);
            StartTrucks = J.StrList(s, "trucks") ?? new List<string>();
            StartMachines = J.StrList(s, "machines") ?? new List<string>();
            StartBalerPetKg = J.D(s, "baler_pet_kg", 0);
            if (J.Obj(s, "fill_pct") is JObj fp) foreach (var kv in fp) StartFill[kv.Key] = J.D(kv.Value);

            var m = J.Obj(b, "materials");
            for (int i = 0; i < Mats.N; i++)
            {
                var o = J.Obj(m, Mats.Ids[i]);
                if (o == null) { Errors.Add("balance: material " + Mats.Ids[i]); continue; }
                Materials[i] = new MaterialDef { Id = Mats.Ids[i], VolL = J.D(o, "vol_l"), Kg = J.D(o, "kg"), BaleKg = J.D(o, "bale_kg"), BalePrice = J.D(o, "bale_price") };
            }
            foreach (var a in J.Arr(b, "archetypes") ?? new List<object>())
                Archetypes.Add(new ArchetypeDef
                {
                    Id = J.S(a, "id"), Min = J.I(a, "min"), Max = J.I(a, "max"), Mix = Dbl(a, "mix", new[] { 1.0, 0, 0 }),
                    Weekend = J.D(a, "weekend", 1), Evening = J.D(a, "evening", 1),
                });
            HourCurve = Dbl(b, "hour_curve", HourCurve);
            WeekendMult = J.D(b, "weekend_mult", 1.25);
            if (J.Obj(b, "points") is JObj pk)
                foreach (var kv in pk)
                    PointKinds[kv.Key] = new PointKindDef
                    {
                        Id = kv.Key, CapL = J.D(kv.Value, "cap_l"), LeaseDay = J.D(kv.Value, "lease_day"), JamPer1000 = J.D(kv.Value, "jam_per_1000"),
                        Citizens = J.B(kv.Value, "citizens"), ShopFee = J.D(kv.Value, "shop_fee"), PickupBonus = J.D(kv.Value, "pickup_bonus"),
                        Mix = Dbl(kv.Value, "mix", new[] { 0.6, 0.25, 0.15 }),
                    };
            CompactorPet = J.D(b, "compactor_pet", 2.5);
            CompactorJamMult = J.D(b, "compactor_jam_mult", 1.6);
            if (J.Obj(b, "vehicles") is JObj vs)
                foreach (var kv in vs)
                {
                    Vehicles[kv.Key] = new VehicleDef
                    {
                        Id = kv.Key, VolL = J.D(kv.Value, "vol_l"), Kg = J.D(kv.Value, "kg"), SpeedKmh = J.D(kv.Value, "speed_kmh", 30),
                        CostKm = J.D(kv.Value, "cost_km"), ServiceMin = J.D(kv.Value, "service_min", 6), Alley = J.B(kv.Value, "alley"),
                        PetCompress = J.D(kv.Value, "pet_compress", 1), Price = J.D(kv.Value, "price"), Research = J.S(kv.Value, "research"),
                    };
                    VehicleOrder.Add(kv.Key);
                }
            var u = J.Obj(b, "unload");
            UnloadBaseMin = J.D(u, "base_min", 8); UnloadPerContainerMin = J.D(u, "per_container_min", 0.0025);
            BreakdownPerKm = J.D(b, "breakdown_per_km", 0.0015);
            GlassBreakPerKm = J.D(b, "glass_break_per_km", 0.0006);

            var e = J.Obj(b, "economy");
            Fee = J.D(e, "fee", 1); BulkFee = J.D(e, "bulk_fee", 0.5);
            FeeRatingMin = J.D(e, "fee_rating_min", 0.85); FeeRatingSpan = J.D(e, "fee_rating_span", 0.3);
            DriverWage = J.D(e, "driver_wage", 110); TechWage = J.D(e, "tech_wage", 90);
            DepotPower = J.D(e, "depot_power", 35); LinePower = J.D(e, "line_power", 15);
            RatingAlpha = J.D(e, "rating_alpha", 0.3); TurnedRatingFreeDays = J.I(e, "turned_rating_free_days", 3);

            var d = J.Obj(b, "depot");
            CountPerHour = J.D(d, "count_per_hour", 420); LinesStart = J.I(d, "lines_start", 1); LinesMax = J.I(d, "lines_max", 3);
            BalerKgPerHour = J.D(d, "baler_kg_per_hour", 90); BalerJamPerBale = J.D(d, "baler_jam_per_bale", 0.02);
            YardCap = J.I(d, "yard_cap", 12); IntakeCap = J.I(d, "intake_cap", 2500); GlassBayT = J.D(d, "glass_bay_t", 3);
            TechSpeedKmh = J.D(d, "tech_speed_kmh", 30); TechFixMin = J.D(d, "tech_fix_min", 20);

            if (J.Obj(b, "prices") is JObj pr) foreach (var kv in pr) Prices[kv.Key] = J.D(kv.Value);

            var dp = J.Obj(b, "dispatcher");
            DispatcherSlots = J.I(dp, "slots", 3); PassSlots = J.I(dp, "pass_slots", 1); PlusSlots = J.I(dp, "plus_slots", 3);
            DispatchEveryMin = J.I(dp, "every_min", 15); MaxStops = J.I(dp, "max_stops", 8);

            var st = J.Obj(b, "stars");
            StarService = Dbl(st, "service", StarService); StarEfficiency = Dbl(st, "efficiency", StarEfficiency); StarThroughput = Dbl(st, "throughput", StarThroughput);

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
            DayIncomeFloor = J.D(b, "day_income_floor", 600);
        }

        void ReadMap(object m)
        {
            if (m == null) return;
            RoadFactor = J.D(m, "road_factor", 1.2);
            foreach (var d in J.Arr(m, "districts") ?? new List<object>())
                Districts.Add(new DistrictDef { Id = J.S(d, "id"), Chapter = J.I(d, "chapter"), Rating = J.D(d, "rating"), Cost = J.D(d, "cost"), Archetypes = Dbl(d, "archetypes", new[] { 1.0 }) });
            foreach (var n in J.Arr(m, "nodes") ?? new List<object>())
            {
                var nd = new NodeDef
                {
                    Index = Nodes.Count, Id = J.S(n, "id"), X = J.D(n, "x"), Y = J.D(n, "y"), District = J.S(n, "district"), Kind = J.S(n, "kind"),
                    Site = J.S(n, "site"), Rate = J.D(n, "rate"), Alley = J.B(n, "alley"), EventSite = J.B(n, "event"),
                };
                if (NodeIndex.ContainsKey(nd.Id)) { Errors.Add("map: duplicate node " + nd.Id); continue; }
                NodeIndex[nd.Id] = nd.Index;
                Nodes.Add(nd);
                if (nd.Kind == "depot") DepotNode = nd.Index;
            }
            foreach (var e in J.Arr(m, "edges") ?? new List<object>())
            {
                if (!(e is List<object> l) || l.Count < 2) { Errors.Add("map: bad edge"); continue; }
                int a = Node(J.Str(l[0])), b = Node(J.Str(l[1]));
                if (a < 0 || b < 0) { Errors.Add("map: edge to unknown node " + J.Str(l[0]) + "-" + J.Str(l[1])); continue; }
                double dx = Nodes[a].X - Nodes[b].X, dy = Nodes[a].Y - Nodes[b].Y;
                Edges.Add(new EdgeDef { A = a, B = b, Km = Math.Sqrt(dx * dx + dy * dy) * RoadFactor, Alley = l.Count > 2 && J.Str(l[2]) == "alley" });
            }
        }

        void ReadResearch(object r)
        {
            if (r == null) return;
            foreach (var n in J.Arr(r, "nodes") ?? new List<object>())
            {
                var d = new ResearchDef { Id = J.S(n, "id"), Branch = J.S(n, "branch"), Cost = J.D(n, "cost"), Minutes = J.D(n, "min"), District = J.S(n, "district"), Requires = J.StrList(n, "requires") ?? new List<string>() };
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
                    Id = J.S(n, "id"), Weight = J.D(n, "weight", 1), MinDay = J.I(n, "min_day", 1), District = J.S(n, "district"), Days = J.I(n, "days", 1),
                    Mult = J.D(n, "mult", 1), From = J.I(n, "from"), To = J.I(n, "to"), BusyMin = J.I(n, "busy_min", 40), Min = J.I(n, "min", 100),
                    Max = J.I(n, "max", 200), BrokenMin = J.I(n, "broken_min", 90),
                });
        }

        void Validate()
        {
            if (DepotNode < 0) Errors.Add("map: no depot");
            if (Archetypes.Count == 0) Errors.Add("balance: no archetypes");
            foreach (var d in Districts) if (d.Archetypes.Length != Archetypes.Count) Errors.Add("map: district " + d.Id + " archetype weights");
            foreach (var n in Nodes)
            {
                if (District(n.District) == null) Errors.Add("map: node " + n.Id + " unknown district");
                if (n.Kind == "site" && !PointKinds.ContainsKey(n.Site == "machine" ? "machine_s" : n.Site)) Errors.Add("map: node " + n.Id + " site kind");
            }
            foreach (var id in StartMachines) if (Node(id) < 0) Errors.Add("balance: start machine " + id);
            foreach (var t in StartTrucks) if (!Vehicles.ContainsKey(t)) Errors.Add("balance: start truck " + t);
            foreach (var r in Research)
            {
                foreach (var q in r.Requires) if (!ResearchById.ContainsKey(q)) Errors.Add("research: " + r.Id + " requires unknown " + q);
                if (r.Minutes > 20) Errors.Add("research: " + r.Id + " longer than 20 min");
            }
            foreach (var v in Vehicles.Values) if (v.Research != "" && !ResearchById.ContainsKey(v.Research)) Errors.Add("vehicle " + v.Id + " research " + v.Research);
            foreach (var id in new[] { "offline_x2", "emergency_empty", "fuel_voucher", "baler_rescue", "daily_bonus" })
                if (!Rewards.ContainsKey(id)) Errors.Add("balance: reward " + id);
            foreach (var mat in Materials) if (mat == null || mat.BaleKg <= 0 || mat.VolL <= 0) { Errors.Add("balance: materials"); break; }
        }
    }
}
