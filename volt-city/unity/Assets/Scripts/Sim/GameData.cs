// Volt City sim core — engine independent (no UnityEngine). C# 9 / .NET Standard 2.1.
using System;
using System.Collections.Generic;
using System.IO;

namespace VoltCity.Sim
{
    public sealed class DistrictDef
    {
        public string Id, Profile, Corridor, Site;
        public int Order, Priority, Chapter;
        public double RatingReq, PeakMw;
        public long Hookup;
        public bool Backup;
    }

    public sealed class PlantDef
    {
        public string Id, Kind, Source, Site, NeedsDistrict;
        public double[] CapacityMw, ReservoirMwh, InflowMw, EnergyMwh, Upkeep;
        public int MinPct, EffPct;
        public double DelayS, RampPctS, StartS, Fuel, Co2;
        public bool Governor, Trip;
    }

    public sealed class CorridorDef
    {
        public string Id, District, Site;
        public double[] CapacityMw;
    }

    public sealed class TieDef
    {
        public string Id, A, B;
    }

    public sealed class InvestmentDef
    {
        public string Id, Kind, Target;
        public int Order, Level, Chapter;
        public long Cost, Gain, ValueMilli;
    }

    public sealed class EventDef
    {
        public string Id, Kind, NeedsPlant, District, Parent;
        public bool Negative, PerUnit, Forecast;
        public double Chance;
        public int MinChapter, FromH, ToH, PartPct, RepairS, WindPct, SolarPct, GasPct, FromMin, ToMin, AtMin, TariffPct, BonusShiftPct, PctFixed;
        public int[] StartH, DurH, Faults, Pct, Shifts, DurS;
        public List<string> NeedsWeather = new List<string>();
    }

    public sealed class CalendarItem
    {
        public string Event;
        public int Every, First;
    }

    public sealed class ChapterDef
    {
        public int Id;
        public string OpensWith, GoalHookup;
        public int GoalGoodShifts, GoalMinStars;
        public double GoalRating;
        public List<CalendarItem> Calendar = new List<CalendarItem>();
    }

    public sealed class WeatherKind
    {
        public string Id, Temp;
        public double Wind, Swing, Solar, Inflow, Weight;
    }

    public sealed class TempDef
    {
        public int FromH, ToH, Pct;
    }

    public sealed class Placement
    {
        public string Id;
        public int Cap, CooldownS;
        public bool Cash;
        public double Share;
    }

    /// <summary>
    /// Every data table from data/*.json, typed and indexed (pure, no engine). JSON numbers are converted once here,
    /// so sim code never compares raw JSON values (LL Driftwood P7: 4 in [1.0, 2.0, 4.0] was false in GDScript).
    /// </summary>
    public sealed class GameData
    {
        public static readonly string[] Files = { "balance", "districts", "plants", "lines", "roads", "investments", "events", "chapters", "weather", "names", "iap" };
        public static readonly string[] Plants = { "steam", "gas", "hydro", "wind", "solar", "battery" };
        public const string Package = "com.coddykit.voltcity";

        public readonly Dictionary<string, JObj> Raw = new Dictionary<string, JObj>();
        public readonly List<DistrictDef> Districts = new List<DistrictDef>();          // in hookup order
        public readonly Dictionary<string, DistrictDef> District = new Dictionary<string, DistrictDef>();
        public readonly Dictionary<string, double[]> Profiles = new Dictionary<string, double[]>(); // 25 hourly values
        public readonly PlantDef[] PlantDefs = new PlantDef[6];                       // in Plants order
        public readonly Dictionary<string, PlantDef> Plant = new Dictionary<string, PlantDef>();
        public readonly List<CorridorDef> Corridors = new List<CorridorDef>();
        public readonly Dictionary<string, CorridorDef> Corridor = new Dictionary<string, CorridorDef>();
        public readonly List<TieDef> Ties = new List<TieDef>();
        public readonly Dictionary<string, TieDef> Tie = new Dictionary<string, TieDef>();
        public readonly List<InvestmentDef> Investments = new List<InvestmentDef>();   // in order 0..49
        public readonly Dictionary<string, InvestmentDef> Investment = new Dictionary<string, InvestmentDef>();
        public readonly List<EventDef> Events = new List<EventDef>();
        public readonly Dictionary<string, EventDef> Event = new Dictionary<string, EventDef>();
        public readonly List<ChapterDef> Chapters = new List<ChapterDef>();
        public readonly List<WeatherKind> Weather = new List<WeatherKind>();
        public readonly Dictionary<string, WeatherKind> WeatherById = new Dictionary<string, WeatherKind>();
        public readonly Dictionary<string, TempDef> Temp = new Dictionary<string, TempDef>();
        public readonly Dictionary<string, Dictionary<string, int>> RoadDist = new Dictionary<string, Dictionary<string, int>>();
        public readonly Dictionary<string, double[]> RoadNodes = new Dictionary<string, double[]>();
        public readonly List<string> CrewNames = new List<string>();
        public readonly List<string> IapProducts = new List<string>();
        public readonly List<string> AdFreeProducts = new List<string>();
        public readonly Dictionary<string, double> ConsumableDays = new Dictionary<string, double>();
        public readonly Dictionary<string, Placement> Placements = new Dictionary<string, Placement>();
        public readonly List<string> Errors = new List<string>();

        // balance.json (typed)
        public int StepUs, ShiftS, StepsPerHour, StartMin, FirstShiftStartMin, Speed4, AutosaveS;
        public int[] Speeds;
        public long StartCash;
        public List<string> StartDistricts = new List<string>();
        public Dictionary<string, int> StartPlants = new Dictionary<string, int>();
        public int StartSteamSteps, StartRatingX100;
        public string StartBatteryMode;
        public double TariffPerMwh;
        public int BandPct, ShedPct, ShedAfterS, OverPct, OverTripAfterS, RestoreAfterS, RestoreMarginPct, GovernorPct, HospitalBackupS;
        public double BackupFuel;
        public bool AutoRestore;
        public double LineHeatPerS, LineCoolPerS;
        public int LineWarnPct, LineRecloseS;
        public int GrowthPerShiftPm, GrowthMaxPm;
        public double CrewSpeedMps;
        public int[] CrewSpeedUpgradesPct;
        public int CrewUpkeep, CrewJobCost, CrewReturnAfterS;
        public int[] DrPct;
        public int DrDurS, DrCooldownS, DrCost, ImportPct, ImportDurS;
        public int RatingZeroPm, RatingFullPm, RatingWindow, ThreeStarPm, TwoStarPm;
        public double Co2Three, Co2Two;
        public int OfflineCapH, OfflinePlusCapH, OfflineMinS;
        public double OfflineShare, OfflinePlusRate;
        public double DayShifts;
        public long DayIncomeFloor;
        public double InvBase, InvMult, InvPaybackFirst, InvPaybackLast, InvValueScale;
        public List<long[]> InvStageBase = new List<long[]>();
        public double RewardSingleMaxDay, RewardDailyMaxDay;
        public int PurchasesPerUtcDay, ReceiptsMax;
        public int ReviewMinPlayS, ReviewMaxPlayS;
        // events.json caps
        public int MaxNegative, TutorialShifts, LineRepairS, TransformerRepairS;
        public bool NoStormAfterOneStar;
        public double Ch1Scale;
        public int[] TreeTrimCutPct;
        // weather.json
        public int PersistencePct, NoisePct;
        public double SolarRiseH, SolarSetH, StormWind, StormSolar, StormInflow;
        public string Depot;

        public int Steps => (int)((long)ShiftS * 1000000L / StepUs);

        static readonly Dictionary<string, GameData> cache = new Dictionary<string, GameData>();

        /// <summary>Loads data/*.json from a directory (EditMode tests, tools). Cached per path.</summary>
        public static GameData LoadDirectory(string dir)
        {
            lock (cache)
            {
                if (cache.TryGetValue(dir, out var hit)) return hit;
                var gd = FromTexts(name =>
                {
                    string p = Path.Combine(dir, name + ".json");
                    return File.Exists(p) ? File.ReadAllText(p) : null;
                });
                cache[dir] = gd;
                return gd;
            }
        }

        /// <summary>Builds from file contents (the Unity layer passes Resources/data TextAssets).</summary>
        public static GameData FromTexts(Func<string, string> read)
        {
            var gd = new GameData();
            var all = new System.Text.StringBuilder();
            foreach (var f in Files)
            {
                string text = read(f);
                all.Append(f).Append('\n').Append(text ?? "").Append('\n');
                var parsed = text == null ? null : Json.TryParse(text) as JObj;
                if (parsed == null)
                {
                    gd.Errors.Add("data/" + f + ".json missing or invalid");
                    continue;
                }
                gd.Raw[f] = parsed;
            }
            gd.Fingerprint = Simulation.Fnv64(all.ToString());
            if (gd.Errors.Count == 0)
            {
                try { gd.Build(); }
                catch (Exception e) { gd.Errors.Add("data build failed: " + e.Message); }
            }
            return gd;
        }

        public bool Ok => Errors.Count == 0;

        /// <summary>FNV-1a 64 of every data file as loaded (cross-process determinism compares runs on the same data only).</summary>
        public string Fingerprint { get; private set; } = "";

        void Build()
        {
            var b = Raw["balance"];
            var t = J.Obj(b, "time");
            StepUs = J.I(t, "step_us"); ShiftS = J.I(t, "shift_s"); StepsPerHour = J.I(t, "steps_per_hour"); StartMin = J.I(t, "start_min");
            FirstShiftStartMin = J.I(t, "first_shift_start_min"); Speeds = J.IntArr(t, "speeds"); Speed4 = J.I(t, "speed4"); AutosaveS = J.I(t, "autosave_s");
            var st = J.Obj(b, "start");
            StartCash = J.L(st, "cash"); StartDistricts = J.StrList(st, "districts");
            var sp = J.Obj(st, "plants");
            foreach (var k in sp.Keys) StartPlants[k] = J.I(sp[k]);
            StartSteamSteps = J.I(st, "steam_steps"); StartBatteryMode = J.S(st, "battery_mode"); StartRatingX100 = J.I(st, "rating_x100");
            TariffPerMwh = J.D(b, "tariff_per_mwh");
            var g = J.Obj(b, "grid");
            BandPct = J.I(g, "band_pct"); ShedPct = J.I(g, "shed_pct"); ShedAfterS = J.I(g, "shed_after_s"); OverPct = J.I(g, "over_pct");
            OverTripAfterS = J.I(g, "over_trip_after_s"); RestoreAfterS = J.I(g, "restore_after_s"); RestoreMarginPct = J.I(g, "restore_margin_pct");
            GovernorPct = J.I(g, "governor_pct"); HospitalBackupS = J.I(g, "hospital_backup_s"); BackupFuel = J.D(g, "backup_fuel"); AutoRestore = J.B(g, "auto_restore", true);
            var ln = J.Obj(b, "line");
            LineHeatPerS = J.D(ln, "heat_per_s"); LineCoolPerS = J.D(ln, "cool_per_s"); LineWarnPct = J.I(ln, "warn_pct"); LineRecloseS = J.I(ln, "reclose_s");
            var gr = J.Obj(b, "growth");
            GrowthPerShiftPm = J.I(gr, "per_shift_pm"); GrowthMaxPm = J.I(gr, "max_pm");
            var cr = J.Obj(b, "crew");
            CrewSpeedMps = J.D(cr, "speed_mps"); CrewSpeedUpgradesPct = J.IntArr(cr, "speed_upgrades_pct"); CrewUpkeep = J.I(cr, "upkeep");
            CrewJobCost = J.I(cr, "job_cost"); CrewReturnAfterS = J.I(cr, "return_after_s");
            var dr = J.Obj(b, "dr");
            DrPct = J.IntArr(dr, "pct"); DrDurS = J.I(dr, "dur_s"); DrCooldownS = J.I(dr, "cooldown_s"); DrCost = J.I(dr, "cost");
            var im = J.Obj(b, "import");
            ImportPct = J.I(im, "pct"); ImportDurS = J.I(im, "dur_s");
            var ra = J.Obj(b, "rating");
            RatingZeroPm = J.I(ra, "u_zero_pm"); RatingFullPm = J.I(ra, "u_full_pm"); RatingWindow = J.I(ra, "window");
            var stars = J.Obj(b, "stars");
            ThreeStarPm = J.I(stars, "three_pm"); TwoStarPm = J.I(stars, "two_pm");
            var co = J.Obj(b, "co2_stars");
            Co2Three = J.D(co, "three"); Co2Two = J.D(co, "two");
            var of = J.Obj(b, "offline");
            OfflineCapH = J.I(of, "cap_h"); OfflineShare = J.D(of, "share"); OfflinePlusCapH = J.I(of, "plus_cap_h"); OfflinePlusRate = J.D(of, "plus_rate"); OfflineMinS = J.I(of, "min_s");
            var dy = J.Obj(b, "day");
            DayShifts = J.D(dy, "shifts"); DayIncomeFloor = J.L(dy, "income_floor");
            var inv = J.Obj(b, "investment");
            InvBase = J.D(inv, "base"); InvMult = J.D(inv, "mult"); InvPaybackFirst = J.D(inv, "payback_first"); InvPaybackLast = J.D(inv, "payback_last");
            InvValueScale = J.D(inv, "value_scale", 1.0);
            foreach (var row in J.Arr(inv, "stage_base"))
                if (row is List<object> r && r.Count == 2) InvStageBase.Add(new[] { J.L(r[0]), J.L(r[1]) });
            var rw = J.Obj(b, "rewards");
            RewardSingleMaxDay = J.D(rw, "single_max_day"); RewardDailyMaxDay = J.D(rw, "daily_max_day");
            var pl = J.Obj(rw, "placements");
            foreach (var k in pl.Keys)
            {
                var p = pl[k];
                Placements[k] = new Placement { Id = k, Cap = J.I(p, "cap"), CooldownS = J.I(p, "cooldown_s"), Cash = J.B(p, "cash"), Share = J.D(p, "share") };
            }
            var pu = J.Obj(b, "purchases");
            PurchasesPerUtcDay = J.I(pu, "per_utc_day"); ReceiptsMax = J.I(pu, "receipts_max");
            var rv = J.Obj(b, "review");
            ReviewMinPlayS = J.I(rv, "min_play_s"); ReviewMaxPlayS = J.I(rv, "max_play_s");

            // districts + profiles
            foreach (var o in J.Arr(Raw["districts"], "districts"))
            {
                var d = new DistrictDef
                {
                    Id = J.S(o, "id"), Order = J.I(o, "order"), RatingReq = J.D(o, "rating_req"), Hookup = J.L(o, "hookup"), PeakMw = J.D(o, "peak_mw"),
                    Profile = J.S(o, "profile"), Priority = J.I(o, "priority"), Corridor = J.S(o, "corridor"), Site = J.S(o, "site"), Chapter = J.I(o, "chapter"),
                    Backup = J.B(o, "backup")
                };
                Districts.Add(d);
                District[d.Id] = d;
            }
            Districts.Sort((x, y) => x.Order.CompareTo(y.Order));
            var prof = J.Obj(Raw["districts"], "profiles");
            foreach (var k in prof.Keys)
            {
                if (k.StartsWith("_", StringComparison.Ordinal)) continue;
                var vals = J.DblArr(prof, k);
                var arr = new double[vals.Length + 1];
                Array.Copy(vals, arr, vals.Length);
                arr[vals.Length] = vals.Length > 0 ? vals[0] : 0;
                Profiles[k] = arr;
            }
            // plants
            foreach (var o in J.Arr(Raw["plants"], "plants"))
            {
                var p = new PlantDef
                {
                    Id = J.S(o, "id"), Kind = J.S(o, "kind"), Source = J.S(o, "source"), Site = J.S(o, "site"), NeedsDistrict = J.S(o, "needs_district"),
                    CapacityMw = J.DblArr(o, "capacity_mw"), ReservoirMwh = J.DblArr(o, "reservoir_mwh"), InflowMw = J.DblArr(o, "inflow_mw"),
                    EnergyMwh = J.DblArr(o, "energy_mwh"), Upkeep = J.DblArr(o, "upkeep"), MinPct = J.I(o, "min_pct"), EffPct = J.I(o, "eff_pct", 100),
                    DelayS = J.D(o, "delay_s"), RampPctS = J.D(o, "ramp_pct_s"), StartS = J.D(o, "start_s"), Fuel = J.D(o, "fuel"), Co2 = J.D(o, "co2"),
                    Governor = J.B(o, "governor"), Trip = J.B(o, "trip")
                };
                Plant[p.Id] = p;
            }
            for (int i = 0; i < Plants.Length; i++)
            {
                if (!Plant.TryGetValue(Plants[i], out var p)) throw new Exception("plant missing: " + Plants[i]);
                PlantDefs[i] = p;
            }
            // lines
            foreach (var o in J.Arr(Raw["lines"], "corridors"))
            {
                var c = new CorridorDef { Id = J.S(o, "id"), District = J.S(o, "district"), Site = J.S(o, "site"), CapacityMw = J.DblArr(o, "capacity_mw") };
                Corridors.Add(c);
                Corridor[c.Id] = c;
            }
            foreach (var o in J.Arr(Raw["lines"], "ties"))
            {
                var tie = new TieDef { Id = J.S(o, "id"), A = J.S(o, "a"), B = J.S(o, "b") };
                Ties.Add(tie);
                Tie[tie.Id] = tie;
            }
            // investments: integer cost from the curve, value (contract level, milli) from cost / payback / expected base
            var list = J.Arr(Raw["investments"], "investments");
            int n = list.Count;
            foreach (var o in list)
            {
                var x = new InvestmentDef
                {
                    Id = J.S(o, "id"), Kind = J.S(o, "kind"), Target = J.S(o, "target"), Level = J.I(o, "level"), Order = J.I(o, "order"), Chapter = J.I(o, "chapter")
                };
                int i = x.Order;
                x.Cost = (long)Math.Round(InvBase * Math.Pow(InvMult, i));
                double payback = InvPaybackFirst + (InvPaybackLast - InvPaybackFirst) * i / Math.Max(n - 1, 1);
                double baseNet = 1.0;
                foreach (var stg in InvStageBase) if (i >= stg[0]) baseNet = stg[1];
                x.Gain = (long)Math.Round(x.Cost / payback);
                x.ValueMilli = Math.Max(1L, (long)Math.Round(1000.0 * x.Gain / baseNet * InvValueScale));
                Investments.Add(x);
                Investment[x.Id] = x;
            }
            Investments.Sort((p, q) => p.Order.CompareTo(q.Order));
            // events
            var ev = Raw["events"];
            var caps = J.Obj(ev, "caps");
            MaxNegative = J.I(caps, "max_negative"); NoStormAfterOneStar = J.B(caps, "no_storm_after_one_star"); TutorialShifts = J.I(caps, "tutorial_shifts");
            Ch1Scale = J.D(caps, "ch1_scale");
            var fa = J.Obj(ev, "faults");
            LineRepairS = J.I(fa, "line_repair_s"); TransformerRepairS = J.I(fa, "transformer_repair_s"); TreeTrimCutPct = J.IntArr(fa, "tree_trim_cut_pct");
            foreach (var o in J.Arr(ev, "events"))
            {
                var e = new EventDef
                {
                    Id = J.S(o, "id"), Kind = J.S(o, "kind"), Negative = J.B(o, "negative"), Chance = J.D(o, "chance"), MinChapter = J.I(o, "min_chapter", 1),
                    StartH = J.IntArr(o, "start_h"), DurH = J.IntArr(o, "dur_h"), Faults = J.IntArr(o, "faults"), FromH = J.I(o, "from_h"), ToH = J.I(o, "to_h"),
                    Shifts = J.IntArr(o, "shifts"), DurS = J.IntArr(o, "dur_s"), PerUnit = J.B(o, "per_unit"), PartPct = J.I(o, "part_pct"),
                    RepairS = J.I(o, "repair_s"), WindPct = J.I(o, "wind_pct"), SolarPct = J.I(o, "solar_pct"), NeedsPlant = J.S(o, "needs_plant"),
                    GasPct = J.I(o, "gas_pct"), District = J.S(o, "district"), FromMin = J.I(o, "from_min"), ToMin = J.I(o, "to_min"), AtMin = J.I(o, "at_min"),
                    TariffPct = J.I(o, "tariff_pct"), BonusShiftPct = J.I(o, "bonus_shift_pct"), Parent = J.S(o, "parent"), Forecast = J.B(o, "forecast"),
                    NeedsWeather = J.StrList(o, "needs_weather")
                };
                // pct is a [lo, hi] range on random events and a single number on scheduled ones
                var pv = J.Get(o, "pct");
                if (pv is List<object>) e.Pct = J.IntArr(o, "pct");
                else { e.PctFixed = J.I(pv); e.Pct = new[] { e.PctFixed, e.PctFixed }; }
                if (J.Get(o, "dur_s") != null && !(J.Get(o, "dur_s") is List<object>)) { int ds = J.I(o, "dur_s"); e.DurS = new[] { ds, ds }; }
                Events.Add(e);
                Event[e.Id] = e;
            }
            // chapters
            foreach (var o in J.Arr(Raw["chapters"], "chapters"))
            {
                var goal = J.Obj(o, "goal");
                var c = new ChapterDef
                {
                    Id = J.I(o, "id"), OpensWith = J.S(o, "opens_with"), GoalHookup = J.S(goal, "hookup"), GoalGoodShifts = J.I(goal, "good_shifts"),
                    GoalMinStars = J.I(goal, "min_stars"), GoalRating = J.D(goal, "rating")
                };
                foreach (var it in J.Arr(o, "calendar"))
                    c.Calendar.Add(new CalendarItem { Event = J.S(it, "event"), Every = J.I(it, "every"), First = J.I(it, "first") });
                Chapters.Add(c);
            }
            Chapters.Sort((x, y) => x.Id.CompareTo(y.Id));
            // weather (file order matters: the weighted pick walks it)
            var w = Raw["weather"];
            var kinds = J.Obj(w, "kinds");
            foreach (var k in kinds.Keys)
            {
                var o = kinds[k];
                var wk = new WeatherKind { Id = k, Wind = J.D(o, "wind"), Swing = J.D(o, "swing"), Solar = J.D(o, "solar"), Inflow = J.D(o, "inflow"), Temp = J.S(o, "temp"), Weight = J.D(o, "weight") };
                Weather.Add(wk);
                WeatherById[k] = wk;
            }
            var tp = J.Obj(w, "temp");
            foreach (var k in tp.Keys) Temp[k] = new TempDef { FromH = J.I(tp[k], "from_h"), ToH = J.I(tp[k], "to_h"), Pct = J.I(tp[k], "pct") };
            var storm = J.Obj(w, "storm");
            StormWind = J.D(storm, "wind"); StormSolar = J.D(storm, "solar"); StormInflow = J.D(storm, "inflow");
            PersistencePct = J.I(w, "persistence_pct"); SolarRiseH = J.D(w, "solar_rise_h"); SolarSetH = J.D(w, "solar_set_h"); NoisePct = J.I(w, "noise_pct");
            // roads
            var rd = Raw["roads"];
            Depot = J.S(rd, "depot");
            var nodes = J.Obj(rd, "nodes");
            foreach (var k in nodes.Keys)
            {
                var xy = nodes[k] as List<object>;
                RoadNodes[k] = new[] { J.D(xy[0]), J.D(xy[1]) };
            }
            AllPairs(J.Arr(rd, "edges"));
            CrewNames.AddRange(J.StrList(Raw["names"], "crew"));
            var iap = Raw["iap"];
            // P4: products became {slug: {...}} (Driftwood schema); the sold list is "order" (P1 kept a plain id list)
            IapProducts.AddRange(J.StrList(iap, J.Arr(iap, "order") != null ? "order" : "products"));
            AdFreeProducts.AddRange(J.StrList(iap, "ad_free_products"));
            var cd = J.Obj(iap, "consumable_days");
            foreach (var k in cd.Keys) ConsumableDays[k] = J.D(cd[k]);
            Validate();
        }

        void AllPairs(List<object> edges)
        {
            var adj = new Dictionary<string, List<KeyValuePair<string, int>>>();
            foreach (var k in RoadNodes.Keys) adj[k] = new List<KeyValuePair<string, int>>();
            foreach (var e in edges)
            {
                var pair = e as List<object>;
                string a = J.Str(pair[0]), z = J.Str(pair[1]);
                if (!RoadNodes.ContainsKey(a) || !RoadNodes.ContainsKey(z)) { Errors.Add("road edge to unknown node " + a + "-" + z); continue; }
                double dx = RoadNodes[a][0] - RoadNodes[z][0], dy = RoadNodes[a][1] - RoadNodes[z][1];
                int len = (int)Math.Round(Math.Sqrt(dx * dx + dy * dy));
                adj[a].Add(new KeyValuePair<string, int>(z, len));
                adj[z].Add(new KeyValuePair<string, int>(a, len));
            }
            var keys = new List<string>(RoadNodes.Keys);
            keys.Sort(StringComparer.Ordinal);
            foreach (var src in keys)
            {
                var dist = new Dictionary<string, int> { [src] = 0 };
                var open = new List<string> { src };
                while (open.Count > 0)
                {
                    int best = 0;
                    for (int i = 1; i < open.Count; i++) if (dist[open[i]] < dist[open[best]]) best = i;
                    string u = open[best];
                    open.RemoveAt(best);
                    foreach (var kv in adj[u])
                    {
                        int nd = dist[u] + kv.Value;
                        if (!dist.TryGetValue(kv.Key, out int old) || nd < old)
                        {
                            if (!dist.ContainsKey(kv.Key)) open.Add(kv.Key);
                            dist[kv.Key] = nd;
                        }
                    }
                }
                RoadDist[src] = dist;
            }
        }

        void Validate()
        {
            if (Districts.Count != 8) Errors.Add("8 districts expected");
            if (Investments.Count != 50) Errors.Add("50 investments expected");
            for (int i = 0; i < Investments.Count; i++) if (Investments[i].Order != i) Errors.Add("investment order gap at " + i);
            if (Events.Count != 15) Errors.Add("15 events expected");
            if (Chapters.Count != 5) Errors.Add("5 chapters expected");
            foreach (var p in PlantDefs) if (p.CapacityMw.Length != 3) Errors.Add("3 levels expected: " + p.Id);
            foreach (var d in Districts)
            {
                if (!Corridor.ContainsKey(d.Corridor)) Errors.Add("district corridor unknown: " + d.Id);
                if (!Profiles.ContainsKey(d.Profile)) Errors.Add("district profile unknown: " + d.Id);
                if (!Reachable(d.Site)) Errors.Add("district site not on the road graph: " + d.Id);
            }
            foreach (var c in Corridors)
            {
                if (c.CapacityMw.Length != 3) Errors.Add("corridor needs 3 capacities: " + c.Id);
                if (!Reachable(c.Site)) Errors.Add("corridor site not on the road graph: " + c.Id);
            }
            foreach (var t in Ties) if (!Corridor.ContainsKey(t.A) || !Corridor.ContainsKey(t.B)) Errors.Add("tie unknown corridor: " + t.Id);
            foreach (var x in Investments)
            {
                switch (x.Kind)
                {
                    case "plant": if (!Plant.ContainsKey(x.Target)) Errors.Add("investment plant unknown: " + x.Id); break;
                    case "line": if (!District.ContainsKey(x.Target)) Errors.Add("investment district unknown: " + x.Id); break;
                    case "substation": if (!Tie.ContainsKey(x.Target)) Errors.Add("investment tie unknown: " + x.Id); break;
                }
            }
        }

        bool Reachable(string node) => RoadDist.TryGetValue(Depot ?? "", out var d) && d.ContainsKey(node ?? "");

        public bool HasSpeed(int x)
        {
            foreach (var v in Speeds) if (v == x) return true;
            return false;
        }

        public int PlantIndex(string id) => Array.IndexOf(Plants, id);

        public int RoadDistance(string a, string b) =>
            RoadDist.TryGetValue(a ?? "", out var d) && d.TryGetValue(b ?? "", out int v) ? v : 0;
    }
}
