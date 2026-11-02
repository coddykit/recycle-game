// Grill Convoy sim core — engine independent (no UnityEngine). C# 9 / .NET Standard 2.1.
// Typed view of the data files in Assets/Resources/data (balance.json, map.json, research.json, events.json).
// The Unity layer passes a loader (name -> text); tests read the files from disk. Errors are collected, never thrown.
using System;
using System.Collections.Generic;

namespace GrillConvoy.Sim
{
    /// <summary>The three stock items a truck carries and the commissary preps.</summary>
    public static class Stock
    {
        public const int Patty = 0, Bun = 1, Side = 2, N = 3;
        public static readonly string[] Ids = { "patty", "bun", "side" };
        public static int Index(string id) => Array.IndexOf(Ids, id);
    }

    /// <summary>Crew roles (one stage each, the runner floats).</summary>
    public static class Roles
    {
        public const string Griller = "griller", Assembler = "assembler", Cashier = "cashier", Runner = "runner";
        public static readonly string[] All = { Griller, Assembler, Cashier, Runner };
        public static bool Valid(string r) => Array.IndexOf(All, r) >= 0;
    }

    /// <summary>The 8 truck stations (layout tiles).</summary>
    public static class Stations
    {
        public const string Grill = "grill", Assembly = "assembly", Window = "window", Toaster = "toaster", Fryer = "fryer",
            Sauce = "sauce", Fridge = "fridge", Wrap = "wrap";
        public static readonly string[] All = { Grill, Assembly, Window, Toaster, Fryer, Sauce, Fridge, Wrap };
        public static bool Valid(string s) => Array.IndexOf(All, s) >= 0;
    }

    public sealed class StockDef { public string Id; public int Batch; public double Cost, PrepMin, Spoil; public int Cap; }
    public sealed class ArchetypeDef { public string Id; public int Min, Max; public double Patience, Side; public double Mean => (Min + Max) * 0.5; }
    public sealed class CardDef { public string Id, Research; public double Price, Work; public int Patties; public double[] Appeal; public int Until; }
    public sealed class StationDef { public string Id, Research; public double Price, Rate; public int Max; }
    public sealed class VehicleDef { public string Id; public int W, H, WExt, HExt, Crew; public double SpeedKmh, CostKm, Price; public int[] Cap = new int[Stock.N]; }
    public sealed class DistrictDef { public string Id; public int Chapter; public double Fame, Cost; }
    public sealed class NodeDef
    {
        public int Index; public string Id, District, Kind, Curve; public double X, Y, Rate, Permit, Weekend = 1; public double[] Arch;
        public Dictionary<string, double> Weather = new Dictionary<string, double>();
        public bool Rival, Match;
        public bool IsPitch => Kind == "pitch";
        public double WeatherMult(string w) => Weather.TryGetValue(w ?? "", out var m) ? m : 1.0;
    }
    public sealed class EdgeDef { public int A, B; public double Km; public bool Alley; }
    public sealed class ResearchDef { public string Id, Branch, District; public double Cost, Minutes; public List<string> Requires = new List<string>(); }
    public sealed class EventDef { public string Id, Kind; public double Weight, Mult = 1, ScoutMult = 1, PromoMult = 1, Level, Rep; public int MinDay; }
    public sealed class RewardDef { public string Id; public int PerDay; public long CooldownS; public double Days; public int Amount; }

    public sealed class ServiceDef
    {
        public double Unstaffed = 0.5, ExtraStaff = 0.3, RunnerBoost = 0.25, WalkPerTile = 0.08, ToasterAdj = 1.15, SauceAdj = 1.1, FridgeAdj = 1.1,
            WrapAdj = 1.25, FryerAdj = 1.2, MenuPenalty = 0.05, SideWork = 0.25, OrderAheadShare = 0.3, OrderAheadPatience = 3,
            AllHandsMult = 1.5, AllHandsMin = 12, AllHandsCooldown = 90, GreaseSafe = 160, FlarePerPatty = 0.00004, FlareMin = 20,
            FlareWastePerMin = 0.4, PropanePerPatty = 0.0025, PropaneLow = 0.15, PropaneSwap = 40, PropanePrice = 30, CleanMin = 4,
            RestockMin = 10, ConvBase = 0.45, ConvAppeal = 0.35, ConvCard = 0.05, HelperMin = 60, FirstDaysDemand = 0.85;
        public int QueueCap = 14, QueueCapWindow = 6;
    }

    public sealed class GameData
    {
        public readonly List<string> Errors = new List<string>();
        public bool Ok => Errors.Count == 0;

        // time
        public int DayStart = 600, DayEnd = 1200, FreeMaxSpeed = 2, PaidMaxSpeed = 4, NightPrepMin = 240;
        public double GameMinPerRealS = 2.5, AutosaveS = 30;
        // start
        public double StartCash = 1500, StartFame = 30;
        public string StartPitch = "", StartTruck = "starter";
        public List<string> StartCrew = new List<string>(), StartMenu = new List<string>();
        public int[] StartLoad = new int[Stock.N], StartCommissary = new int[Stock.N];
        public readonly List<(string type, int x, int y)> StartLayout = new List<(string, int, int)>();

        public readonly StockDef[] StockDefs = new StockDef[Stock.N];
        public readonly List<ArchetypeDef> Archetypes = new List<ArchetypeDef>();
        public readonly Dictionary<string, double[]> Curves = new Dictionary<string, double[]>();
        public readonly List<CardDef> Cards = new List<CardDef>();
        public double SidePrice = 2.5;
        public double[] TierCost = { 0.2, 0.5, 1.1 }, TierAppeal = { 0.92, 1, 1.12 }, TierRating = { -0.4, 0, 0.5 };
        public readonly Dictionary<string, StationDef> StationDefs = new Dictionary<string, StationDef>();
        public readonly ServiceDef Svc = new ServiceDef();
        public readonly Dictionary<string, VehicleDef> Vehicles = new Dictionary<string, VehicleDef>();
        public readonly List<string> VehicleOrder = new List<string>();
        // economy
        public double Wage = 70, HirePrice = 300, ExtendPrice = 3000, FameAlpha = 0.3, RepStart = 30, RepGain = 8, RepRating = 3, DayIncomeFloor = 600;
        public int FameFreeDays = 3, MaxTrucks = 2;
        // stars
        public double[] StarService = { 0.25, 0.12, 0.05 }, StarStock = { 0.2, 0.08, 0.02 }, StarSpeed = { 9, 6, 3.5 };
        public double StarBusy;   // a 3rd service star needs the line busy at least this share of the open time (0 = off)
        // weather / match / events
        public string[] WeatherIds = { "sunny", "cloudy", "hot", "rain" };
        public double[] WeatherWeights = { 0.45, 0.25, 0.15, 0.15 };
        public int[] WeatherMinDay = { 1, 1, 4, 4 };
        public int MatchEvery = 3, MatchFrom = 1020, MatchTo = 1110, HalftimeFrom = 1065, HalftimeTo = 1080;
        public double MatchMult = 2.2, HalftimeMult = 5, MatchPatience = 0.6;
        public int EventFreeDays = 3;
        public double[] EventsPerDay = { 0, 0.5, 0.8, 1.1 };
        // offline
        public double OfflineMinS = 120, OfflineCapH = 4, OfflineCapHPlus = 10, OfflineRate = 0.5, OfflineRatePlus = 0.75, OfflineDaysPerHour = 0.5;
        public readonly Dictionary<string, RewardDef> Rewards = new Dictionary<string, RewardDef>();
        public double RewardSingleMaxDays = 0.2;
        // rules (Dispatcher / Head cook automation)
        public int RuleSlots = 1, PassSlots = 1, PlusSlots = 2, Presets = 1, PassPresets = 1;

        // map
        public double RoadFactor = 1.25;
        public readonly List<DistrictDef> Districts = new List<DistrictDef>();
        public readonly List<NodeDef> Nodes = new List<NodeDef>();
        public readonly List<EdgeDef> Edges = new List<EdgeDef>();
        public readonly Dictionary<string, int> NodeIndex = new Dictionary<string, int>();
        public readonly List<int> Pitches = new List<int>();
        public int DepotNode = -1;
        public RoadGraph Graph;

        public readonly List<ResearchDef> Research = new List<ResearchDef>();
        public readonly Dictionary<string, ResearchDef> ResearchById = new Dictionary<string, ResearchDef>();
        public readonly List<EventDef> Events = new List<EventDef>();

        public DistrictDef District(string id) => Districts.Find(d => d.Id == id);
        public int Node(string id) => NodeIndex.TryGetValue(id ?? "", out int i) ? i : -1;
        public CardDef Card(string id) => Cards.Find(c => c.Id == id);
        public int CardIndex(string id) => Cards.FindIndex(c => c.Id == id);
        public double DayMinutes => DayEnd - DayStart;
        /// <summary>Real seconds of one service day at 1x.</summary>
        public double RealSecondsPerDay => DayMinutes / Math.Max(0.01, GameMinPerRealS);
        public double[] Curve(string id) => Curves.TryGetValue(id ?? "", out var c) ? c : new[] { 1.0 };
        public EventDef Event(string id) => Events.Find(e => e.Id == id);

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

        static int[] Ints(object o, string key, int len)
        {
            var r = new int[len];
            var a = J.IntArr(o, key);
            for (int i = 0; i < len && i < a.Length; i++) r[i] = a[i];
            return r;
        }

        void ReadBalance(object b)
        {
            if (b == null) return;
            var t = J.Obj(b, "time");
            DayStart = J.I(t, "day_start", 600); DayEnd = J.I(t, "day_end", 1200);
            GameMinPerRealS = J.D(t, "game_min_per_real_s", 2.5); AutosaveS = J.D(t, "autosave_s", 30);
            FreeMaxSpeed = J.I(t, "free_max_speed", 2); PaidMaxSpeed = J.I(t, "paid_max_speed", 4);
            NightPrepMin = J.I(t, "night_prep_min", 240);

            var s = J.Obj(b, "start");
            StartCash = J.D(s, "cash", 1500); StartFame = J.D(s, "fame", 30);
            StartPitch = J.S(s, "pitch"); StartTruck = J.S(s, "truck", "starter");
            StartCrew = J.StrList(s, "crew"); StartMenu = J.StrList(s, "menu");
            StartLoad = Ints(s, "load", Stock.N); StartCommissary = Ints(s, "commissary", Stock.N);
            foreach (var l in J.Arr(s, "layout") ?? new List<object>())
                if (l is List<object> a && a.Count >= 3) StartLayout.Add((J.Str(a[0]), J.I(a[1]), J.I(a[2])));

            var st = J.Obj(b, "stock");
            for (int i = 0; i < Stock.N; i++)
            {
                var o = J.Obj(st, Stock.Ids[i]);
                if (o == null) { Errors.Add("balance: stock " + Stock.Ids[i]); continue; }
                StockDefs[i] = new StockDef { Id = Stock.Ids[i], Batch = J.I(o, "batch", 60), Cost = J.D(o, "cost"), PrepMin = J.D(o, "prep_min", 30), Spoil = J.D(o, "spoil"), Cap = J.I(o, "cap", 500) };
            }
            foreach (var a in J.Arr(b, "archetypes") ?? new List<object>())
                Archetypes.Add(new ArchetypeDef { Id = J.S(a, "id"), Min = J.I(a, "min", 1), Max = J.I(a, "max", 1), Patience = J.D(a, "patience", 8), Side = J.D(a, "side") });
            if (J.Obj(b, "curves") is JObj cv) foreach (var kv in cv) Curves[kv.Key] = J.DblArr(cv, kv.Key);
            foreach (var c in J.Arr(b, "menu") ?? new List<object>())
                Cards.Add(new CardDef
                {
                    Id = J.S(c, "id"), Price = J.D(c, "price"), Work = J.D(c, "work", 1), Patties = J.I(c, "patties", 1),
                    Appeal = Dbl(c, "appeal", new[] { 1.0 }), Research = J.S(c, "research"), Until = J.I(c, "until", 0),
                });
            SidePrice = J.D(b, "side_price", 2.5);
            var ti = J.Obj(b, "tiers");
            TierCost = Dbl(ti, "cost", TierCost); TierAppeal = Dbl(ti, "appeal", TierAppeal); TierRating = Dbl(ti, "rating", TierRating);
            if (J.Obj(b, "stations") is JObj sts)
                foreach (var kv in sts)
                    StationDefs[kv.Key] = new StationDef { Id = kv.Key, Price = J.D(kv.Value, "price"), Max = J.I(kv.Value, "max", 1), Rate = J.D(kv.Value, "rate"), Research = J.S(kv.Value, "research") };

            var sv = J.Obj(b, "service");
            var S = Svc;
            S.Unstaffed = J.D(sv, "unstaffed", S.Unstaffed); S.ExtraStaff = J.D(sv, "extra_staff", S.ExtraStaff); S.RunnerBoost = J.D(sv, "runner_boost", S.RunnerBoost);
            S.WalkPerTile = J.D(sv, "walk_per_tile", S.WalkPerTile); S.ToasterAdj = J.D(sv, "toaster_adj", S.ToasterAdj); S.SauceAdj = J.D(sv, "sauce_adj", S.SauceAdj);
            S.FridgeAdj = J.D(sv, "fridge_adj", S.FridgeAdj); S.WrapAdj = J.D(sv, "wrap_adj", S.WrapAdj); S.FryerAdj = J.D(sv, "fryer_adj", S.FryerAdj);
            S.MenuPenalty = J.D(sv, "menu_penalty", S.MenuPenalty); S.SideWork = J.D(sv, "side_work", S.SideWork);
            S.QueueCap = J.I(sv, "queue_cap", S.QueueCap); S.QueueCapWindow = J.I(sv, "queue_cap_window", S.QueueCapWindow);
            S.OrderAheadShare = J.D(sv, "order_ahead_share", S.OrderAheadShare); S.OrderAheadPatience = J.D(sv, "order_ahead_patience", S.OrderAheadPatience);
            S.AllHandsMult = J.D(sv, "all_hands_mult", S.AllHandsMult); S.AllHandsMin = J.D(sv, "all_hands_min", S.AllHandsMin); S.AllHandsCooldown = J.D(sv, "all_hands_cooldown", S.AllHandsCooldown);
            S.GreaseSafe = J.D(sv, "grease_safe", S.GreaseSafe); S.FlarePerPatty = J.D(sv, "flare_per_patty", S.FlarePerPatty); S.FlareMin = J.D(sv, "flare_min", S.FlareMin);
            S.FlareWastePerMin = J.D(sv, "flare_waste_per_min", S.FlareWastePerMin); S.PropanePerPatty = J.D(sv, "propane_per_patty", S.PropanePerPatty);
            S.PropaneLow = J.D(sv, "propane_low", S.PropaneLow); S.PropaneSwap = J.D(sv, "propane_swap", S.PropaneSwap); S.PropanePrice = J.D(sv, "propane_price", S.PropanePrice);
            S.CleanMin = J.D(sv, "clean_min", S.CleanMin); S.RestockMin = J.D(sv, "restock_min", S.RestockMin);
            S.ConvBase = J.D(sv, "conv_base", S.ConvBase); S.ConvAppeal = J.D(sv, "conv_appeal", S.ConvAppeal); S.ConvCard = J.D(sv, "conv_card", S.ConvCard);
            S.HelperMin = J.D(sv, "helper_min", S.HelperMin); S.FirstDaysDemand = J.D(sv, "first_days_demand", S.FirstDaysDemand);

            if (J.Obj(b, "vehicles") is JObj vs)
                foreach (var kv in vs)
                {
                    Vehicles[kv.Key] = new VehicleDef
                    {
                        Id = kv.Key, W = J.I(kv.Value, "w", 4), H = J.I(kv.Value, "h", 3), WExt = J.I(kv.Value, "w_ext", 5), HExt = J.I(kv.Value, "h_ext", 4),
                        Crew = J.I(kv.Value, "crew", 3), SpeedKmh = J.D(kv.Value, "speed_kmh", 22), CostKm = J.D(kv.Value, "cost_km", 1.2),
                        Price = J.D(kv.Value, "price"), Cap = Ints(kv.Value, "cap", Stock.N),
                    };
                    VehicleOrder.Add(kv.Key);
                }

            var e = J.Obj(b, "economy");
            Wage = J.D(e, "wage", 70); HirePrice = J.D(e, "hire", 300); ExtendPrice = J.D(e, "extend", 3000);
            FameAlpha = J.D(e, "fame_alpha", 0.3); FameFreeDays = J.I(e, "fame_free_days", 3); MaxTrucks = J.I(e, "max_trucks", 2);
            RepStart = J.D(e, "rep_start", 30); RepGain = J.D(e, "rep_gain", 8); RepRating = J.D(e, "rep_rating", 3);

            var sr = J.Obj(b, "stars");
            StarService = Dbl(sr, "service", StarService); StarStock = Dbl(sr, "stock", StarStock); StarSpeed = Dbl(sr, "speed", StarSpeed); StarBusy = J.D(sr, "busy", 0);

            var w = J.Obj(b, "weather");
            var wids = J.StrList(w, "ids");
            if (wids.Count > 0) WeatherIds = wids.ToArray();
            WeatherWeights = Dbl(w, "weights", WeatherWeights);
            WeatherMinDay = Ints(w, "min_day", WeatherIds.Length);

            var m = J.Obj(b, "match");
            MatchEvery = J.I(m, "every", 3); MatchFrom = J.I(m, "from", 1020); MatchTo = J.I(m, "to", 1110); MatchMult = J.D(m, "mult", 2.2);
            HalftimeFrom = J.I(m, "halftime_from", 1065); HalftimeTo = J.I(m, "halftime_to", 1080); HalftimeMult = J.D(m, "halftime_mult", 5); MatchPatience = J.D(m, "patience", 0.6);

            var ev = J.Obj(b, "events");
            EventFreeDays = J.I(ev, "free_days", 3); EventsPerDay = Dbl(ev, "per_day", EventsPerDay);

            var of = J.Obj(b, "offline");
            OfflineMinS = J.D(of, "min_s", 120); OfflineCapH = J.D(of, "cap_h", 4); OfflineCapHPlus = J.D(of, "cap_h_plus", 10);
            OfflineRate = J.D(of, "rate", 0.5); OfflineRatePlus = J.D(of, "rate_plus", 0.75); OfflineDaysPerHour = J.D(of, "days_per_hour", 0.5);

            if (J.Obj(b, "rewards") is JObj rw)
                foreach (var kv in rw)
                {
                    if (!(kv.Value is JObj)) { if (kv.Key == "single_max_days") RewardSingleMaxDays = J.D(kv.Value, 0.2); continue; }
                    Rewards[kv.Key] = new RewardDef { Id = kv.Key, PerDay = J.I(kv.Value, "per_day", 1), CooldownS = J.L(kv.Value, "cooldown_s"), Days = J.D(kv.Value, "days"), Amount = J.I(kv.Value, "amount") };
                }
            var ru = J.Obj(b, "rules");
            RuleSlots = J.I(ru, "slots", 1); PassSlots = J.I(ru, "pass_slots", 1); PlusSlots = J.I(ru, "plus_slots", 2);
            Presets = J.I(ru, "presets", 1); PassPresets = J.I(ru, "pass_presets", 1);
            DayIncomeFloor = J.D(b, "day_income_floor", 600);
        }

        void ReadMap(object m)
        {
            if (m == null) return;
            RoadFactor = J.D(m, "road_factor", 1.25);
            foreach (var d in J.Arr(m, "districts") ?? new List<object>())
                Districts.Add(new DistrictDef { Id = J.S(d, "id"), Chapter = J.I(d, "chapter"), Fame = J.D(d, "fame"), Cost = J.D(d, "cost") });
            foreach (var n in J.Arr(m, "nodes") ?? new List<object>())
            {
                var nd = new NodeDef
                {
                    Index = Nodes.Count, Id = J.S(n, "id"), X = J.D(n, "x"), Y = J.D(n, "y"), District = J.S(n, "district"), Kind = J.S(n, "kind"),
                    Curve = J.S(n, "curve"), Rate = J.D(n, "rate"), Permit = J.D(n, "permit"), Weekend = J.D(n, "weekend", 1),
                    Arch = Dbl(n, "arch", new[] { 1.0 }), Rival = J.B(n, "rival"), Match = J.B(n, "match"),
                };
                if (J.Obj(n, "weather") is JObj wo) foreach (var kv in wo) nd.Weather[kv.Key] = J.D(kv.Value, 1);
                if (NodeIndex.ContainsKey(nd.Id)) { Errors.Add("map: duplicate node " + nd.Id); continue; }
                NodeIndex[nd.Id] = nd.Index;
                Nodes.Add(nd);
                if (nd.Kind == "depot") DepotNode = nd.Index;
                if (nd.IsPitch) Pitches.Add(nd.Index);
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
                var d = new ResearchDef { Id = J.S(n, "id"), Branch = J.S(n, "branch"), Cost = J.D(n, "cost"), Minutes = J.D(n, "min"), District = J.S(n, "district"), Requires = J.StrList(n, "requires") };
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
                    Id = J.S(n, "id"), Kind = J.S(n, "kind", "random"), Weight = J.D(n, "weight", 1), MinDay = J.I(n, "min_day", 1), Mult = J.D(n, "mult", 1),
                    ScoutMult = J.D(n, "scout_mult", 1), PromoMult = J.D(n, "promo_mult", 1), Level = J.D(n, "level", 0.12), Rep = J.D(n, "rep", 40),
                });
        }

        void Validate()
        {
            if (DepotNode < 0) Errors.Add("map: no commissary");
            if (Archetypes.Count == 0) Errors.Add("balance: no archetypes");
            if (Pitches.Count == 0) Errors.Add("map: no pitches");
            foreach (var n in Nodes)
            {
                if (District(n.District) == null) Errors.Add("map: node " + n.Id + " unknown district");
                if (n.IsPitch && n.Arch.Length != Archetypes.Count) Errors.Add("map: pitch " + n.Id + " archetype weights");
                if (n.IsPitch && !Curves.ContainsKey(n.Curve)) Errors.Add("map: pitch " + n.Id + " curve " + n.Curve);
            }
            foreach (var c in Curves) if (c.Value.Length != (DayEnd - DayStart) / 60) Errors.Add("balance: curve " + c.Key + " needs one value per hour");
            foreach (var c in Cards)
            {
                if (c.Appeal.Length != Archetypes.Count) Errors.Add("balance: card " + c.Id + " appeal");
                if (c.Research != "" && !ResearchById.ContainsKey(c.Research)) Errors.Add("balance: card " + c.Id + " research " + c.Research);
            }
            foreach (var id in StartMenu) if (Card(id) == null) Errors.Add("balance: start menu " + id);
            foreach (var r in StartCrew) if (!Roles.Valid(r)) Errors.Add("balance: start crew " + r);
            foreach (var l in StartLayout) if (!StationDefs.ContainsKey(l.type)) Errors.Add("balance: start layout " + l.type);
            foreach (var s in Stations.All) if (!StationDefs.ContainsKey(s)) Errors.Add("balance: station " + s);
            foreach (var s in StationDefs.Values) if (s.Research != "" && !ResearchById.ContainsKey(s.Research)) Errors.Add("balance: station " + s.Id + " research " + s.Research);
            if (Node(StartPitch) < 0 || !Nodes[Node(StartPitch)].IsPitch) Errors.Add("balance: start pitch " + StartPitch);
            if (!Vehicles.ContainsKey(StartTruck)) Errors.Add("balance: start truck " + StartTruck);
            foreach (var r in Research)
            {
                foreach (var q in r.Requires) if (!ResearchById.ContainsKey(q)) Errors.Add("research: " + r.Id + " requires unknown " + q);
                if (r.Minutes > 20) Errors.Add("research: " + r.Id + " longer than 20 min");
                if (!string.IsNullOrEmpty(r.District) && District(r.District) == null) Errors.Add("research: " + r.Id + " district");
            }
            foreach (var id in Simulation.RewardPlacements)
                if (!Rewards.ContainsKey(id)) Errors.Add("balance: reward " + id);
            foreach (var sd in StockDefs) if (sd == null || sd.Batch <= 0) { Errors.Add("balance: stock"); break; }
            if (WeatherWeights.Length != WeatherIds.Length) Errors.Add("balance: weather weights");
        }
    }
}
