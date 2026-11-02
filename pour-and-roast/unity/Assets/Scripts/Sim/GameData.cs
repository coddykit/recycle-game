// Pour & Roast sim core — engine independent (no UnityEngine). C# 9 / .NET Standard 2.1.
// Typed view of the data files in Assets/Resources/data (balance.json, venues.json, menu.json, research.json, events.json).
// The Unity layer passes a loader (name -> text); tests read the files from disk. Errors are collected, never thrown.
using System;
using System.Collections.Generic;

namespace PourAndRoast.Sim
{
    public static class Tasks
    {
        public const string Shot = "shot", Steam = "steam", Pour = "pour", Pastry = "pastry";
        public static bool Valid(string t) => t == Shot || t == Steam || t == Pour || t == Pastry;
    }

    public static class Stations
    {
        public const string Espresso = "espresso", Milk = "milk", Any = "any";
        public static readonly string[] All = { Espresso, Milk, Any };
        public static bool Valid(string s) => s == Espresso || s == Milk || s == Any;
    }

    public static class Roles
    {
        public const string Barista = "barista", Cashier = "cashier", Runner = "runner", Roaster = "roaster";
        public static readonly string[] All = { Barista, Cashier, Runner, Roaster };
        public static bool Valid(string r) => Array.IndexOf(All, r) >= 0;
    }

    public static class Profiles
    {
        public const string Light = "light", Medium = "medium", Dark = "dark";
        public static readonly string[] All = { Light, Medium, Dark };
        public static bool Valid(string p) => Array.IndexOf(All, p) >= 0;
        public static int Index(string p) => Array.IndexOf(All, p);
    }

    public static class Weathers
    {
        public const string Sunny = "sunny", Rain = "rain", Heat = "heat", Fog = "fog";
        public static readonly string[] All = { Sunny, Rain, Heat, Fog };
        public static bool Valid(string w) => Array.IndexOf(All, w) >= 0;
    }

    public sealed class MenuItemDef
    {
        public int Index;
        public string Id, Kind;
        public int Tier;
        public double Price, Beans, Pop, PourMin;
        public string[] Tasks = new string[0];
        public bool Milk, Iced, Prep, Showcase;
        public bool IsPastry => Kind == "pastry";
        public bool HasShot => Array.IndexOf(Tasks, Sim.Tasks.Shot) >= 0;
    }

    public sealed class CustomerDef
    {
        public string Id;
        public int ItemsMin, ItemsMax;
        public double Patience, Oat, Iced, Pastry, Pre, Tip, RatingMin, Weekend;
        public bool Vip;
    }

    public sealed class VenueDef
    {
        public string Id;
        public int Chapter, Heads0, HeadsMax, Wands0, WandsMax, SeatsMax, Ice0;
        public double Rating, Cost, PerHour, PriceMult, Rent, AutoFloor;
        public double[] Weights = new double[0];
    }

    public sealed class RoleDef { public string Id, Research = ""; public double Wage, Price; public int Max; }
    public sealed class TraitDef { public string Id; public double Speed = 1, Q, Tip; }
    public sealed class OriginDef { public int Index; public string Id, Best, Research = ""; public double Q, Price; }
    public sealed class ContractDef { public string Id, Research = ""; public double Kg, Pay, MinQ, Rating; public int Every; }
    public sealed class SupplierDef { public string Id; public double Price, Late, Q; }
    public sealed class WeatherDef { public string Id; public double Weight, Walk = 1, Pre = 1, Iced = 1, Tune = 1; }
    public sealed class ResearchDef { public string Id, Branch, Venue = ""; public double Cost, Minutes; public List<string> Requires = new List<string>(); }
    public sealed class EventDef { public string Id, Trigger; public double Weight, Mult; public int MinDay, Days; }
    public sealed class RewardDef { public string Id; public int PerDay; public long CooldownS; public double Days, Minutes; }

    public sealed class GameData
    {
        public readonly List<string> Errors = new List<string>();
        public bool Ok => Errors.Count == 0;

        // time
        public int DayStart = 420, DayEnd = 1080, FreeMaxSpeed = 2, PaidMaxSpeed = 4;
        public double GameMinPerRealS = 3, AutosaveS = 30;
        // start
        public double StartCash = 420, StartRating = 2.5, StartBeansKg = 6, StartBeansQ = 52, StartIce;
        public string StartVenue = "cart", StartSupplier = "regular";
        public List<string> StartStaff = new List<string>();
        public List<string> StartMenu = new List<string>();
        public List<string> StartForecast = new List<string>();
        public double[] StartMilk = { 16, 5 }, StartMilkOrder = { 18, 6 };

        public readonly List<CustomerDef> Customers = new List<CustomerDef>();
        public double[] HourCurve = { 1 };
        public double WeekendMult = 1.2;
        // work
        public double ShotMin = 1.1, SteamMin = 0.9, PourMin = 0.35, PastryMin = 0.2, BatchMult = 1.5, MenuSlow = 0.03, HandoverMin = 1.5, HandoverHostMin = 0.4, PreGrace = 15;
        public int BatchLook = 3, MenuFree = 5, PreLeadMin = 20, PreLeadMax = 40;
        // stocks
        public double SupplierQ = 52, SupplierPrice = 15, SupplierLot = 5, BeanParKg = 4;
        public double MilkPerDrink = 0.2, MilkSpoil = 0.5, MilkLateMin = 180, RunMult = 2, RunMin = 15, RunMinRunner = 5, OatPriceMult = 1.6;
        public double[] RunL = { 10, 4 };
        public readonly Dictionary<string, SupplierDef> Suppliers = new Dictionary<string, SupplierDef>();
        public readonly List<string> SupplierOrder = new List<string>();
        public double IcePerDrink = 0.3, IceBagKg = 10, IceBagPrice = 8;
        public double[] IcePerHour = { 0, 6, 10, 16 }, IceCap = { 0, 24, 40, 60 };
        public double PastryCostShare = 0.45, OvenCostShare = 0.35;
        public int PastryParDefault = 6, PastryParMax = 30;
        // quality
        public double TuneDriftH = 7, TuneOff = 60, TuneBad = 35, TuneAutoFloor = 85, TunePenalty = 0.6, TuneScaleMult = 0.6;
        public double WearPerShot = 0.05, JamFrom = 80, JamPerShot = 0.01, JamAutoFixMin = 25, GrinderServicePrice = 60;
        public double PriceQMin = 0.8, PriceQSpan = 0.4, LevelQ = 2, PourOverBonus = 8, PastryQ = 70, OvenQ = 80;
        // reviews
        public double[] ReviewWait = { 0.5, 0.8, 1.0 }, ReviewWaitPen = { 0, 0.5, 1.0, 1.6 }, ReviewQ = { 75, 60, 45 }, ReviewQPen = { 0, 0.5, 1.2, 2.0 };
        public double LatePen = 1.5, FailPen = 1.2, SubPen = 0.4, DecorReview = 0.1, MusicReview = 0.15;
        public int WallSize = 12, SpreeCount = 3;
        // staff
        public readonly Dictionary<string, RoleDef> Roles = new Dictionary<string, RoleDef>();
        public readonly Dictionary<string, TraitDef> Traits = new Dictionary<string, TraitDef>();
        public readonly List<string> TraitOrder = new List<string>();
        public double LevelSpeed = 0.08;
        public int LevelMax = 5;
        public double[] TrainPrice = { 0, 90, 180, 360, 650 };
        // economy
        public double RatingAlpha = 0.3, FootfallMin = 0.6, FootfallPerStar = 0.16, DayIncomeFloor = 250, BranchShare = 0.5, SeatFootfall = 0.05, TapTip = 0.05;
        public int RatingFreeDays = 3;
        public readonly Dictionary<string, double[]> Prices = new Dictionary<string, double[]>();
        public int RailBase = 6, RailPerLevel = 2;
        // roastery
        public double RoastRealS = 30, RoastLoss = 0.15, SweetFrom = 0.7, SweetTo = 0.86, RoastEarly = 0.6, AssistAt = 0.78, BonusSweet = 10, BonusAssist = 6, BonusPass = 4,
            PenEarly = -15, PenLate = -4, PenAuto = -6, ProfileMatch = 6, LabBonus = 4, LotKg = 10, LotKgStorage = 20, StorageDiscount = 0.9, MarketDrift = 0.05, MarketRevert = 0.3;
        public int RestDays = 1;
        public double[] DrumKg = { 0, 5, 10, 15 };
        public readonly List<OriginDef> Origins = new List<OriginDef>();
        public readonly List<ContractDef> Contracts = new List<ContractDef>();
        public double ContractMissFee = 0.2, ContractMissMult = 0.9, ContractPayFloor = 0.7, PassWholesale = 1.1;
        // weather / prep / events / offline / rewards
        public readonly Dictionary<string, WeatherDef> Weather = new Dictionary<string, WeatherDef>();
        public int PrepHotCups = 12;
        public double PrepHotPrice = 12, PrepIceKg = 10, PrepIcePrice = 8;
        public int EventFreeDays = 3;
        public double OfflineMinS = 120, OfflineCapH = 4, OfflineCapHPlus = 10, OfflineRate = 0.5, OfflineRatePlus = 0.75, OfflineDaysPerHour = 0.5;
        public readonly Dictionary<string, RewardDef> Rewards = new Dictionary<string, RewardDef>();
        public double RewardSingleMaxDays = 0.2;

        public readonly List<VenueDef> Venues = new List<VenueDef>();
        public readonly List<MenuItemDef> Menu = new List<MenuItemDef>();
        public readonly List<ResearchDef> Research = new List<ResearchDef>();
        public readonly Dictionary<string, ResearchDef> ResearchById = new Dictionary<string, ResearchDef>();
        public readonly List<EventDef> Events = new List<EventDef>();

        public VenueDef Venue(string id) => Venues.Find(v => v.Id == id);
        public int MenuIndex(string id) => Menu.FindIndex(m => m.Id == id);
        public MenuItemDef Item(string id) { int i = MenuIndex(id); return i >= 0 ? Menu[i] : null; }
        public int OriginIndex(string id) => Origins.FindIndex(o => o.Id == id);
        public ContractDef Contract(string id) => Contracts.Find(c => c.Id == id);
        public int CustomerIndex(string id) => Customers.FindIndex(c => c.Id == id);
        public double DayMinutes => DayEnd - DayStart;
        /// <summary>Real seconds of one service day at 1x.</summary>
        public double RealSecondsPerDay => DayMinutes / Math.Max(0.01, GameMinPerRealS);
        /// <summary>Price of level `level` of an upgrade (0 when the table has no such level).</summary>
        public double Price(string id, int level) => Prices.TryGetValue(id, out var a) && level >= 0 && level < a.Length ? a[level] : 0;
        public int MaxLevel(string id) => Prices.TryGetValue(id, out var a) ? a.Length - 1 : 0;

        public static GameData FromTexts(Func<string, string> load)
        {
            var gd = new GameData();
            gd.ReadBalance(Parse(gd, load, "balance"));
            gd.ReadVenues(Parse(gd, load, "venues"));
            gd.ReadMenu(Parse(gd, load, "menu"));
            gd.ReadResearch(Parse(gd, load, "research"));
            gd.ReadEvents(Parse(gd, load, "events"));
            gd.Validate();
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
            DayStart = J.I(t, "day_start", 420); DayEnd = J.I(t, "day_end", 1080);
            GameMinPerRealS = J.D(t, "game_min_per_real_s", 3); AutosaveS = J.D(t, "autosave_s", 30);
            FreeMaxSpeed = J.I(t, "free_max_speed", 2); PaidMaxSpeed = J.I(t, "paid_max_speed", 4);

            var s = J.Obj(b, "start");
            StartCash = J.D(s, "cash", 420); StartRating = J.D(s, "rating", 2.5); StartVenue = J.S(s, "venue", "cart");
            StartStaff = J.StrList(s, "staff"); StartMenu = J.StrList(s, "menu"); StartForecast = J.StrList(s, "forecast");
            StartBeansKg = J.D(s, "beans_kg", 6); StartBeansQ = J.D(s, "beans_q", 52); StartIce = J.D(s, "ice", 0);
            StartMilk = Dbl(s, "milk", StartMilk); StartMilkOrder = Dbl(s, "milk_order", StartMilkOrder);
            StartSupplier = J.S(s, "supplier", "regular");

            foreach (var c in J.Arr(b, "customers") ?? new List<object>())
            {
                var items = J.IntArr(c, "items");
                Customers.Add(new CustomerDef
                {
                    Id = J.S(c, "id"), ItemsMin = items.Length > 0 ? items[0] : 1, ItemsMax = items.Length > 1 ? items[1] : 1,
                    Patience = J.D(c, "patience", 10), Oat = J.D(c, "oat"), Iced = J.D(c, "iced"), Pastry = J.D(c, "pastry"), Pre = J.D(c, "pre"),
                    Tip = J.D(c, "tip"), Vip = J.B(c, "vip"), RatingMin = J.D(c, "rating_min"), Weekend = J.D(c, "weekend", 1),
                });
            }
            HourCurve = Dbl(b, "hour_curve", HourCurve);
            WeekendMult = J.D(b, "weekend_mult", 1.2);

            var w = J.Obj(b, "work");
            ShotMin = J.D(w, "shot_min", 1.1); SteamMin = J.D(w, "steam_min", 0.9); PourMin = J.D(w, "pour_min", 0.35); PastryMin = J.D(w, "pastry_min", 0.2);
            BatchMult = J.D(w, "batch_mult", 1.5); BatchLook = J.I(w, "batch_look", 3); MenuFree = J.I(w, "menu_free", 5); MenuSlow = J.D(w, "menu_slow", 0.03);
            HandoverMin = J.D(w, "handover_min", 1.5); HandoverHostMin = J.D(w, "handover_host_min", 0.4); PreGrace = J.D(w, "pre_grace", 15);
            var lead = J.IntArr(w, "pre_lead");
            if (lead.Length >= 2) { PreLeadMin = lead[0]; PreLeadMax = lead[1]; }

            var be = J.Obj(b, "beans");
            SupplierQ = J.D(be, "supplier_q", 52); SupplierPrice = J.D(be, "supplier_price", 15); SupplierLot = J.D(be, "supplier_lot", 5); BeanParKg = J.D(be, "par_kg", 4);
            var mk = J.Obj(b, "milk");
            MilkPerDrink = J.D(mk, "per_drink", 0.2); MilkSpoil = J.D(mk, "spoil", 0.5); MilkLateMin = J.D(mk, "late_min", 180);
            RunMult = J.D(mk, "run_mult", 2); RunL = Dbl(mk, "run_l", RunL); RunMin = J.D(mk, "run_min", 15); RunMinRunner = J.D(mk, "run_min_runner", 5);
            OatPriceMult = J.D(b, "oat_price_mult", 1.6);
            if (J.Obj(b, "suppliers") is JObj sp)
                foreach (var kv in sp)
                {
                    Suppliers[kv.Key] = new SupplierDef { Id = kv.Key, Price = J.D(kv.Value, "price", 1), Late = J.D(kv.Value, "late"), Q = J.D(kv.Value, "q") };
                    SupplierOrder.Add(kv.Key);
                }
            var ic = J.Obj(b, "ice");
            IcePerDrink = J.D(ic, "per_drink", 0.3); IcePerHour = Dbl(ic, "per_hour", IcePerHour); IceCap = Dbl(ic, "cap", IceCap);
            IceBagKg = J.D(ic, "bag_kg", 10); IceBagPrice = J.D(ic, "bag_price", 8);
            var pa = J.Obj(b, "pastry");
            PastryCostShare = J.D(pa, "cost_share", 0.45); OvenCostShare = J.D(pa, "oven_cost_share", 0.35);
            PastryParDefault = J.I(pa, "par_default", 6); PastryParMax = J.I(pa, "par_max", 30);

            var tu = J.Obj(b, "tune");
            TuneDriftH = J.D(tu, "drift_h", 7); TuneOff = J.D(tu, "off", 60); TuneBad = J.D(tu, "bad", 35); TuneAutoFloor = J.D(tu, "auto_floor", 85);
            TunePenalty = J.D(tu, "penalty", 0.6); TuneScaleMult = J.D(tu, "scale_mult", 0.6);
            var gr = J.Obj(b, "grinder");
            WearPerShot = J.D(gr, "wear_per_shot", 0.05); JamFrom = J.D(gr, "jam_from", 80); JamPerShot = J.D(gr, "jam_per_shot", 0.01);
            JamAutoFixMin = J.D(gr, "auto_fix_min", 25); GrinderServicePrice = J.D(gr, "service_price", 60);
            var q = J.Obj(b, "quality");
            PriceQMin = J.D(q, "price_min", 0.8); PriceQSpan = J.D(q, "price_span", 0.4); LevelQ = J.D(q, "level_q", 2);
            PourOverBonus = J.D(q, "pourover_bonus", 8); PastryQ = J.D(q, "pastry_q", 70); OvenQ = J.D(q, "oven_q", 80);
            var rv = J.Obj(b, "reviews");
            ReviewWait = Dbl(rv, "wait", ReviewWait); ReviewWaitPen = Dbl(rv, "wait_pen", ReviewWaitPen); ReviewQ = Dbl(rv, "q", ReviewQ); ReviewQPen = Dbl(rv, "q_pen", ReviewQPen);
            LatePen = J.D(rv, "late_pen", 1.5); FailPen = J.D(rv, "fail_pen", 1.2); SubPen = J.D(rv, "sub_pen", 0.4);
            DecorReview = J.D(rv, "decor", 0.1); MusicReview = J.D(rv, "music", 0.15); WallSize = J.I(rv, "wall", 12); SpreeCount = J.I(rv, "spree", 3);

            var st = J.Obj(b, "staff");
            if (J.Obj(st, "roles") is JObj ro)
                foreach (var kv in ro)
                    Roles[kv.Key] = new RoleDef { Id = kv.Key, Wage = J.D(kv.Value, "wage"), Price = J.D(kv.Value, "price"), Max = J.I(kv.Value, "max", 1), Research = J.S(kv.Value, "research") };
            if (J.Obj(st, "traits") is JObj tr)
                foreach (var kv in tr)
                {
                    Traits[kv.Key] = new TraitDef { Id = kv.Key, Speed = J.D(kv.Value, "speed", 1), Q = J.D(kv.Value, "q"), Tip = J.D(kv.Value, "tip") };
                    TraitOrder.Add(kv.Key);
                }
            LevelSpeed = J.D(st, "level_speed", 0.08); LevelMax = J.I(st, "level_max", 5); TrainPrice = Dbl(st, "train_price", TrainPrice);

            var e = J.Obj(b, "economy");
            RatingAlpha = J.D(e, "rating_alpha", 0.3); RatingFreeDays = J.I(e, "rating_free_days", 3);
            FootfallMin = J.D(e, "footfall_min", 0.6); FootfallPerStar = J.D(e, "footfall_per_star", 0.16);
            BranchShare = J.D(e, "branch_share", 0.5); SeatFootfall = J.D(e, "seat_footfall", 0.05); TapTip = J.D(e, "tap_tip", 0.05);
            if (J.Obj(b, "prices") is JObj pr) foreach (var kv in pr) Prices[kv.Key] = J.DblArr(pr, kv.Key);
            var ra = J.Obj(b, "rail");
            RailBase = J.I(ra, "base", 6); RailPerLevel = J.I(ra, "per_level", 2);

            var r = J.Obj(b, "roast");
            RoastRealS = J.D(r, "real_s", 30); RoastLoss = J.D(r, "loss", 0.15);
            var sw = J.DblArr(r, "sweet");
            if (sw.Length >= 2) { SweetFrom = sw[0]; SweetTo = sw[1]; }
            RoastEarly = J.D(r, "early", 0.6); AssistAt = J.D(r, "assist_at", 0.78);
            BonusSweet = J.D(r, "bonus_sweet", 10); BonusAssist = J.D(r, "bonus_assist", 6); BonusPass = J.D(r, "bonus_pass", 4);
            PenEarly = J.D(r, "pen_early", -15); PenLate = J.D(r, "pen_late", -4); PenAuto = J.D(r, "pen_auto", -6);
            ProfileMatch = J.D(r, "profile_match", 6); LabBonus = J.D(r, "lab_bonus", 4); RestDays = J.I(r, "rest_days", 1);
            DrumKg = Dbl(r, "drum_kg", DrumKg); LotKg = J.D(r, "lot_kg", 10); LotKgStorage = J.D(r, "lot_kg_storage", 20); StorageDiscount = J.D(r, "storage_discount", 0.9);
            MarketDrift = J.D(r, "market_drift", 0.05); MarketRevert = J.D(r, "market_revert", 0.3);
            foreach (var o in J.Arr(b, "origins") ?? new List<object>())
                Origins.Add(new OriginDef { Index = Origins.Count, Id = J.S(o, "id"), Q = J.D(o, "q"), Price = J.D(o, "price"), Best = J.S(o, "best", "medium"), Research = J.S(o, "research") });
            foreach (var c in J.Arr(b, "contracts") ?? new List<object>())
                Contracts.Add(new ContractDef { Id = J.S(c, "id"), Kg = J.D(c, "kg"), Every = Math.Max(1, J.I(c, "every", 3)), Pay = J.D(c, "pay"), MinQ = J.D(c, "min_q"), Rating = J.D(c, "rating"), Research = J.S(c, "research") });
            ContractMissFee = J.D(b, "contract_miss_fee", 0.2); ContractMissMult = J.D(b, "contract_miss_mult", 0.9);
            ContractPayFloor = J.D(b, "contract_pay_floor", 0.7); PassWholesale = J.D(b, "pass_wholesale", 1.1);

            if (J.Obj(b, "weather") is JObj we)
                foreach (var kv in we)
                    Weather[kv.Key] = new WeatherDef { Id = kv.Key, Weight = J.D(kv.Value, "weight"), Walk = J.D(kv.Value, "walk", 1), Pre = J.D(kv.Value, "pre", 1), Iced = J.D(kv.Value, "iced", 1), Tune = J.D(kv.Value, "tune", 1) };
            var pp = J.Obj(b, "prep");
            PrepHotCups = J.I(pp, "hot_cups", 12); PrepHotPrice = J.D(pp, "hot_price", 12); PrepIceKg = J.D(pp, "ice_kg", 10); PrepIcePrice = J.D(pp, "ice_price", 8);
            EventFreeDays = J.I(J.Obj(b, "events"), "free_days", 3);
            var of = J.Obj(b, "offline");
            OfflineMinS = J.D(of, "min_s", 120); OfflineCapH = J.D(of, "cap_h", 4); OfflineCapHPlus = J.D(of, "cap_h_plus", 10);
            OfflineRate = J.D(of, "rate", 0.5); OfflineRatePlus = J.D(of, "rate_plus", 0.75); OfflineDaysPerHour = J.D(of, "days_per_hour", 0.5);
            if (J.Obj(b, "rewards") is JObj rw)
                foreach (var kv in rw)
                {
                    if (!(kv.Value is JObj)) { if (kv.Key == "single_max_days") RewardSingleMaxDays = J.D(kv.Value, 0.2); continue; }
                    Rewards[kv.Key] = new RewardDef { Id = kv.Key, PerDay = J.I(kv.Value, "per_day", 1), CooldownS = J.L(kv.Value, "cooldown_s"), Days = J.D(kv.Value, "days"), Minutes = J.D(kv.Value, "minutes") };
                }
            DayIncomeFloor = J.D(b, "day_income_floor", 250);
        }

        void ReadVenues(object v)
        {
            if (v == null) return;
            foreach (var o in J.Arr(v, "venues") ?? new List<object>())
            {
                var h = J.IntArr(o, "heads"); var w = J.IntArr(o, "wands");
                Venues.Add(new VenueDef
                {
                    Id = J.S(o, "id"), Chapter = J.I(o, "chapter", 1), Rating = J.D(o, "rating"), Cost = J.D(o, "cost"), PerHour = J.D(o, "per_hour"),
                    PriceMult = J.D(o, "price_mult", 1), Rent = J.D(o, "rent"), Heads0 = h.Length > 0 ? h[0] : 1, HeadsMax = h.Length > 1 ? h[1] : 1,
                    Wands0 = w.Length > 0 ? w[0] : 1, WandsMax = w.Length > 1 ? w[1] : 1, SeatsMax = J.I(o, "seats_max", 2), Ice0 = J.I(o, "ice0"),
                    AutoFloor = J.D(o, "auto_floor"), Weights = Dbl(o, "weights", new double[0]),
                });
            }
        }

        void ReadMenu(object m)
        {
            if (m == null) return;
            foreach (var o in J.Arr(m, "items") ?? new List<object>())
            {
                var it = new MenuItemDef
                {
                    Index = Menu.Count, Id = J.S(o, "id"), Kind = J.S(o, "kind", "drink"), Tier = J.I(o, "tier", 1), Price = J.D(o, "price"), Beans = J.D(o, "beans"),
                    Pop = J.D(o, "pop", 1), PourMin = J.D(o, "pour_min"), Tasks = J.StrList(o, "tasks").ToArray(), Milk = J.B(o, "milk"), Iced = J.B(o, "iced"),
                    Prep = J.B(o, "prep"), Showcase = J.B(o, "showcase"),
                };
                if (MenuIndex(it.Id) >= 0) { Errors.Add("menu: duplicate " + it.Id); continue; }
                Menu.Add(it);
            }
        }

        void ReadResearch(object r)
        {
            if (r == null) return;
            foreach (var n in J.Arr(r, "nodes") ?? new List<object>())
            {
                var d = new ResearchDef { Id = J.S(n, "id"), Branch = J.S(n, "branch"), Cost = J.D(n, "cost"), Minutes = J.D(n, "min"), Venue = J.S(n, "venue"), Requires = J.StrList(n, "requires") };
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
                    Id = J.S(n, "id"), Trigger = J.S(n, "trigger"), Weight = J.D(n, "weight", 1), MinDay = J.I(n, "min_day", 1), Days = J.I(n, "days", 1), Mult = J.D(n, "mult", 1),
                });
        }

        void Validate()
        {
            if (Customers.Count == 0) Errors.Add("balance: no customers");
            if (Venues.Count == 0) Errors.Add("venues: none");
            if (Venue(StartVenue) == null) Errors.Add("balance: start venue " + StartVenue);
            foreach (var v in Venues) if (v.Weights.Length != Customers.Count) Errors.Add("venues: " + v.Id + " customer weights");
            foreach (var id in StartMenu) if (MenuIndex(id) < 0) Errors.Add("balance: start menu " + id);
            foreach (var id in StartStaff) if (!Roles.ContainsKey(id)) Errors.Add("balance: start staff " + id);
            foreach (var f in StartForecast) if (!Weather.ContainsKey(f)) Errors.Add("balance: forecast " + f);
            foreach (var w in Weathers.All) if (!Weather.ContainsKey(w)) Errors.Add("balance: weather " + w);
            foreach (var r in Sim.Roles.All) if (!Roles.ContainsKey(r)) Errors.Add("balance: role " + r);
            if (!Suppliers.ContainsKey(StartSupplier)) Errors.Add("balance: supplier " + StartSupplier);
            if (Traits.Count == 0) Errors.Add("balance: traits");
            foreach (var m in Menu)
            {
                if (m.Tasks.Length == 0) Errors.Add("menu: " + m.Id + " no tasks");
                foreach (var t in m.Tasks) if (!Sim.Tasks.Valid(t)) Errors.Add("menu: " + m.Id + " task " + t);
                if (m.Price <= 0) Errors.Add("menu: " + m.Id + " price");
                if (m.Tier < 1 || m.Tier > Venues.Count) Errors.Add("menu: " + m.Id + " tier");
            }
            foreach (var r in Research)
            {
                foreach (var q in r.Requires) if (!ResearchById.ContainsKey(q)) Errors.Add("research: " + r.Id + " requires unknown " + q);
                if (r.Minutes > 20) Errors.Add("research: " + r.Id + " longer than 20 min");
                if (r.Venue != "" && Venue(r.Venue) == null) Errors.Add("research: " + r.Id + " venue " + r.Venue);
            }
            foreach (var o in Origins) { if (o.Research != "" && !ResearchById.ContainsKey(o.Research)) Errors.Add("origin " + o.Id + " research"); if (!Profiles.Valid(o.Best)) Errors.Add("origin " + o.Id + " profile"); }
            foreach (var c in Contracts) if (c.Research != "" && !ResearchById.ContainsKey(c.Research)) Errors.Add("contract " + c.Id + " research");
            foreach (var r in Roles.Values) if (r.Research != "" && !ResearchById.ContainsKey(r.Research)) Errors.Add("role " + r.Id + " research");
            foreach (var id in new[] { "heads", "wands", "grinder", "ice", "rail", "seats", "decor", "drum" }) if (!Prices.ContainsKey(id)) Errors.Add("balance: price " + id);
            foreach (var id in RewardIds) if (!Rewards.ContainsKey(id)) Errors.Add("balance: reward " + id);
            if (HourCurve.Length == 0) Errors.Add("balance: hour curve");
        }

        public static readonly string[] RewardIds = { "offline_x2", "extra_barista", "auto_dialin", "emergency_stock", "crisis_halve", "daily_bonus" };
    }
}
