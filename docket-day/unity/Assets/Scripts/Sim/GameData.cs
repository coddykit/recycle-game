// Docket Day sim core — engine independent (no UnityEngine). C# 9 / .NET Standard 2.1.
// Typed view of the data files in Assets/Resources/data (balance.json, matters.json, office.json, events.json).
// The Unity layer passes a loader (name -> text); tests read the files from disk. Errors are collected, never thrown.
using System;
using System.Collections.Generic;

namespace DocketDay.Sim
{
    /// <summary>Task kinds on a matter. file / evidence / prep / motion take people-hours; hearing is a court date.</summary>
    public static class Kinds
    {
        public const string File = "file", Evidence = "evidence", Prep = "prep", Motion = "motion", Hearing = "hearing";
        public static bool Valid(string k) => k == File || k == Evidence || k == Prep || k == Motion || k == Hearing;
        public static bool Work(string k) => k == File || k == Evidence || k == Prep || k == Motion;
        /// <summary>The skill a kind uses (a motion is paperwork: the filing skill).</summary>
        public static string Skill(string k) => k == Motion ? File : k;
    }

    public static class Fees
    {
        public const string Hourly = "hourly", Retainer = "retainer", Contingency = "contingency";
        public static bool Valid(string f) => f == Hourly || f == Retainer || f == Contingency;
    }

    public sealed class RoleDef
    {
        public string Id;
        public double Hours, Rate, Salary, Hire, Trial, File, Evidence, Prep;
        public int Chapter;
        public double Skill(string kind)
        {
            switch (Kinds.Skill(kind))
            {
                case Kinds.File: return File;
                case Kinds.Evidence: return Evidence;
                case Kinds.Prep: return Prep;
                case Kinds.Hearing: return Trial;
            }
            return 0;
        }
        public bool Lawyer => Trial > 0;
    }

    public sealed class AreaDef
    {
        public string Id, Fee;
        public int Chapter, Claims, Titles, AfterMin, AfterMax;
        public double Success, Value, Retainer, File, Evidence, Prep, HearingH, Weight;
    }

    public sealed class TierDef { public string Id; public double Hours = 1, Fee = 1, Diff, MinStanding; }
    public sealed class JudgeDef { public string Id, Kind; public int Chapter; public double Mod, Settle = 1; }
    public sealed class ChapterDef { public string Id; public int Chapter, Desks, Closed; public double Standing, Cost; }
    public sealed class RoomDef { public string Id; public int Chapter, Desks, Max = 1; public double Cost, BuildS, Prep = 1, File = 1, Evidence = 1, Fatigue = 1, Settle = 1, Odds; }
    public sealed class EventDef { public string Id; public double Weight, Hours; public int MinDay, Minutes; }
    public sealed class RewardDef { public string Id; public int PerDay; public long CooldownS; public double Share; }

    public sealed class GameData
    {
        public readonly List<string> Errors = new List<string>();
        public bool Ok => Errors.Count == 0;

        // time
        public int DayStart = 540, DayEnd = 1080, FreeMaxSpeed = 2, PaidMaxSpeed = 4, HearingSessionMin = 45, RailDays = 10, RailDaysPass = 14, WeekDays = 5;
        public double GameMinPerRealS = 3, AutosaveS = 30;
        public int[] HearingSlots = { 600, 840 };
        // start
        public double StartCash = 1500, StartStanding = 1;
        public List<string> StartStaff = new List<string>();
        // economy
        public double[] Rent = { 40, 120, 260 };
        public double Sanction = 60, VisitingFee = 240, VisitingTrial = 0.03, ReferralFee = 70, WallCost = 300, NoLawyerPenalty = 0.25,
            AutoContinuanceFee = 100, DayIncomeFloor = 500, SettleFactor = 0.6, ContingencyShare = 1.0 / 3.0, LessonShare = 0.15,
            LessonValueShare = 0.05, PuzzleFallback = 0.6, StandingFee = 0.05;
        public int ContinuanceDays = 3, ForgivingDays = 2;
        // odds
        public double OddsBase = 0.4, OddsPrep = 0.25, OddsPuzzle = 0.15, OddsStanding = 0.02, OddsFileMissing = 0.12, OddsEvidenceMissing = 0.08,
            OddsObjection = 0.05, OddsMin = 0.05, OddsMax = 0.95;
        // standing
        public double StWin = 0.22, StSettle = 0.1, StLoss = -0.08, StLate = -0.12, StWalkedOut = -0.04, StNoLawyer = -0.2, StContinuance = -0.06,
            StReferral = 0.03, StMax = 5;
        // fatigue
        public double FatPerHour = 3.5, FatOvertime = 2, FatRecover = 25, FatRecoverFree = 5, FatTired = 70, FatTiredMult = 0.8, FatSpent = 90, FatSpentMult = 0.55;
        // intake
        public double[] Walkins = { 1.6, 2.6, 3.4 };
        public double WalkinPerStanding = 0.15, WalkinPerReceptionist = 0.8, PatienceMin = 150, PatiencePerReceptionist = 90, ConflictChance = 0.15,
            TierMChance = 0.35, TierLChance = 0.2;
        public int MaxReceptionists = 2, QueueMax = 4, MorningWalkins = 1, ConflictChapter = 2, Upcoming = 3;
        // stars
        public double[] StarBilling = { 0.4, 0.6, 0.8 }, StarClients = { 0.6, 0.75, 0.9 };
        // events
        public int EventFreeDays = 3;
        public double[] EventsPerDay = { 0, 0.5, 0.8, 1.0 };
        // offline
        public double OfflineMinS = 120, OfflineCapH = 4, OfflineCapHPlus = 8, OfflineRate = 0.5, OfflineDaysPerHour = 0.5;
        // rewards
        public readonly Dictionary<string, RewardDef> Rewards = new Dictionary<string, RewardDef>();
        public double RewardSingleMaxDays = 0.2;
        // standing instructions (auto-intake rules)
        public int RulesFree = 1, RulesPremium = 1, RulesPass = 1, RulesAutomation = 2;

        // matters
        public readonly List<AreaDef> Areas = new List<AreaDef>();
        public readonly Dictionary<string, TierDef> Tiers = new Dictionary<string, TierDef>();
        public double[] Twists = { 0 };
        public readonly List<JudgeDef> Judges = new List<JudgeDef>();
        public readonly List<string> Parties = new List<string>();
        // office
        public readonly Dictionary<string, RoleDef> Roles = new Dictionary<string, RoleDef>();
        public readonly List<string> RoleOrder = new List<string>();
        public readonly List<ChapterDef> Chapters = new List<ChapterDef>();
        public readonly List<RoomDef> Rooms = new List<RoomDef>();
        public readonly List<EventDef> Events = new List<EventDef>();

        public AreaDef Area(string id) => Areas.Find(a => a.Id == id);
        public int AreaIndex(string id) => Areas.FindIndex(a => a.Id == id);
        public TierDef Tier(string id) => Tiers.TryGetValue(id ?? "", out var t) ? t : null;
        public RoleDef Role(string id) => Roles.TryGetValue(id ?? "", out var r) ? r : null;
        public RoomDef Room(string id) => Rooms.Find(r => r.Id == id);
        public ChapterDef ChapterOf(int n) => Chapters.Find(c => c.Chapter == n);
        public double DayMinutes => DayEnd - DayStart;
        /// <summary>Real seconds of one work day at 1x.</summary>
        public double RealSecondsPerDay => DayMinutes / Math.Max(0.01, GameMinPerRealS);

        public static GameData FromTexts(Func<string, string> load)
        {
            var gd = new GameData();
            gd.ReadBalance(Parse(gd, load, "balance"));
            gd.ReadMatters(Parse(gd, load, "matters"));
            gd.ReadOffice(Parse(gd, load, "office"));
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
            DayStart = J.I(t, "day_start", 540); DayEnd = J.I(t, "day_end", 1080);
            GameMinPerRealS = J.D(t, "game_min_per_real_s", 3); AutosaveS = J.D(t, "autosave_s", 30);
            FreeMaxSpeed = J.I(t, "free_max_speed", 2); PaidMaxSpeed = J.I(t, "paid_max_speed", 4);
            var hs = J.IntArr(t, "hearing_slots");
            if (hs != null && hs.Length > 0) HearingSlots = hs;
            HearingSessionMin = J.I(t, "hearing_session_min", 45); RailDays = J.I(t, "rail_days", 10); RailDaysPass = J.I(t, "rail_days_pass", 14);
            WeekDays = Math.Max(1, J.I(t, "week_days", 5));

            var s = J.Obj(b, "start");
            StartCash = J.D(s, "cash", 1500); StartStanding = J.D(s, "standing", 1);
            StartStaff = J.StrList(s, "staff") ?? new List<string>();

            var e = J.Obj(b, "economy");
            Rent = Dbl(e, "rent", Rent);
            Sanction = J.D(e, "sanction", 60); VisitingFee = J.D(e, "visiting_fee", 240); VisitingTrial = J.D(e, "visiting_trial", 0.03);
            ReferralFee = J.D(e, "referral_fee", 70); WallCost = J.D(e, "wall_cost", 300); ContinuanceDays = J.I(e, "continuance_days", 3);
            NoLawyerPenalty = J.D(e, "no_lawyer_penalty", 0.25); AutoContinuanceFee = J.D(e, "auto_continuance_fee", 100);
            DayIncomeFloor = J.D(e, "day_income_floor", 500); SettleFactor = J.D(e, "settle_factor", 0.6);
            ContingencyShare = J.D(e, "contingency_share", 1.0 / 3.0); LessonShare = J.D(e, "lesson_share", 0.15);
            LessonValueShare = J.D(e, "lesson_value_share", 0.05); PuzzleFallback = J.D(e, "puzzle_fallback", 0.6);
            StandingFee = J.D(e, "standing_fee", 0.05); ForgivingDays = J.I(e, "forgiving_days", 2);

            var o = J.Obj(b, "odds");
            OddsBase = J.D(o, "base", 0.4); OddsPrep = J.D(o, "prep", 0.25); OddsPuzzle = J.D(o, "puzzle", 0.15); OddsStanding = J.D(o, "standing", 0.02);
            OddsFileMissing = J.D(o, "file_missing", 0.12); OddsEvidenceMissing = J.D(o, "evidence_missing", 0.08); OddsObjection = J.D(o, "objection", 0.05);
            OddsMin = J.D(o, "min", 0.05); OddsMax = J.D(o, "max", 0.95);

            var st = J.Obj(b, "standing");
            StWin = J.D(st, "win", 0.22); StSettle = J.D(st, "settle", 0.1); StLoss = J.D(st, "loss", -0.08); StLate = J.D(st, "late", -0.12);
            StWalkedOut = J.D(st, "walked_out", -0.04); StNoLawyer = J.D(st, "no_lawyer", -0.2); StContinuance = J.D(st, "continuance", -0.06);
            StReferral = J.D(st, "referral", 0.03); StMax = J.D(st, "max", 5);

            var f = J.Obj(b, "fatigue");
            FatPerHour = J.D(f, "per_hour", 3.5); FatOvertime = J.D(f, "overtime_mult", 2); FatRecover = J.D(f, "recover", 25);
            FatRecoverFree = J.D(f, "recover_per_free_hour", 5); FatTired = J.D(f, "tired", 70); FatTiredMult = J.D(f, "tired_mult", 0.8);
            FatSpent = J.D(f, "spent", 90); FatSpentMult = J.D(f, "spent_mult", 0.55);

            var i = J.Obj(b, "intake");
            Walkins = Dbl(i, "walkins", Walkins); WalkinPerStanding = J.D(i, "per_standing", 0.15); WalkinPerReceptionist = J.D(i, "per_receptionist", 0.8);
            MaxReceptionists = J.I(i, "max_receptionists", 2); PatienceMin = J.D(i, "patience_min", 150); PatiencePerReceptionist = J.D(i, "patience_per_receptionist", 90);
            QueueMax = J.I(i, "queue_max", 4); MorningWalkins = J.I(i, "morning", 1); ConflictChance = J.D(i, "conflict_chance", 0.15);
            ConflictChapter = J.I(i, "conflict_chapter", 2); TierMChance = J.D(i, "tier_m_chance", 0.35); TierLChance = J.D(i, "tier_l_chance", 0.2);
            Upcoming = Math.Max(1, J.I(i, "upcoming", 3));

            var sr = J.Obj(b, "stars");
            StarBilling = Dbl(sr, "billing", StarBilling); StarClients = Dbl(sr, "clients", StarClients);

            var ev = J.Obj(b, "events");
            EventFreeDays = J.I(ev, "free_days", 3); EventsPerDay = Dbl(ev, "per_day", EventsPerDay);

            var of = J.Obj(b, "offline");
            OfflineMinS = J.D(of, "min_s", 120); OfflineCapH = J.D(of, "cap_h", 4); OfflineCapHPlus = J.D(of, "cap_h_plus", 8);
            OfflineRate = J.D(of, "rate", 0.5); OfflineDaysPerHour = J.D(of, "days_per_hour", 0.5);

            if (J.Obj(b, "rewards") is JObj rw)
                foreach (var kv in rw)
                {
                    if (!(kv.Value is JObj)) { if (kv.Key == "single_max_days") RewardSingleMaxDays = J.D(kv.Value, 0.2); continue; }
                    Rewards[kv.Key] = new RewardDef { Id = kv.Key, PerDay = J.I(kv.Value, "per_day", 1), CooldownS = J.L(kv.Value, "cooldown_s"), Share = J.D(kv.Value, "share") };
                }
            var ru = J.Obj(b, "rules");
            RulesFree = J.I(ru, "free", 1); RulesPremium = J.I(ru, "premium", 1); RulesPass = J.I(ru, "pass", 1); RulesAutomation = J.I(ru, "automation", 2);
        }

        void ReadMatters(object m)
        {
            if (m == null) return;
            foreach (var a in J.Arr(m, "areas") ?? new List<object>())
            {
                var after = J.IntArr(a, "after") ?? new[] { 3, 5 };
                Areas.Add(new AreaDef
                {
                    Id = J.S(a, "id"), Chapter = J.I(a, "chapter", 1), Fee = J.S(a, "fee", Fees.Hourly), Success = J.D(a, "success"), Value = J.D(a, "value"),
                    Retainer = J.D(a, "retainer"), File = J.D(a, "file"), Evidence = J.D(a, "evidence"), Prep = J.D(a, "prep"), HearingH = J.D(a, "hearing_h", 2),
                    AfterMin = after.Length > 0 ? after[0] : 3, AfterMax = after.Length > 1 ? after[1] : (after.Length > 0 ? after[0] : 5),
                    Claims = J.I(a, "claims", 2), Titles = Math.Max(1, J.I(a, "titles", 1)), Weight = J.D(a, "weight", 1),
                });
            }
            if (J.Obj(m, "tiers") is JObj ts)
                foreach (var kv in ts)
                    Tiers[kv.Key] = new TierDef { Id = kv.Key, Hours = J.D(kv.Value, "hours", 1), Fee = J.D(kv.Value, "fee", 1), Diff = J.D(kv.Value, "diff"), MinStanding = J.D(kv.Value, "min_standing") };
            Twists = Dbl(m, "twists", Twists);
            foreach (var j in J.Arr(m, "judges") ?? new List<object>())
                Judges.Add(new JudgeDef { Id = J.S(j, "id"), Chapter = J.I(j, "chapter", 1), Kind = J.S(j, "kind"), Mod = J.D(j, "mod"), Settle = J.D(j, "settle", 1) });
            Parties.AddRange(J.StrList(m, "parties") ?? new List<string>());
        }

        void ReadOffice(object o)
        {
            if (o == null) return;
            if (J.Obj(o, "roles") is JObj rs)
                foreach (var kv in rs)
                    Roles[kv.Key] = new RoleDef
                    {
                        Id = kv.Key, Hours = J.D(kv.Value, "hours", 8), Rate = J.D(kv.Value, "rate"), Salary = J.D(kv.Value, "salary"), Hire = J.D(kv.Value, "hire"),
                        Chapter = J.I(kv.Value, "chapter", 1), Trial = J.D(kv.Value, "trial"), File = J.D(kv.Value, "file"), Evidence = J.D(kv.Value, "evidence"), Prep = J.D(kv.Value, "prep"),
                    };
            RoleOrder.AddRange(J.StrList(o, "role_order") ?? new List<string>(Roles.Keys));
            foreach (var c in J.Arr(o, "chapters") ?? new List<object>())
                Chapters.Add(new ChapterDef { Id = J.S(c, "id"), Chapter = J.I(c, "chapter"), Desks = J.I(c, "desks", 5), Standing = J.D(c, "standing"), Closed = J.I(c, "closed"), Cost = J.D(c, "cost") });
            foreach (var r in J.Arr(o, "rooms") ?? new List<object>())
                Rooms.Add(new RoomDef
                {
                    Id = J.S(r, "id"), Chapter = J.I(r, "chapter", 1), Cost = J.D(r, "cost"), BuildS = J.D(r, "build_s", 60), Desks = J.I(r, "desks"), Max = Math.Max(1, J.I(r, "max", 1)),
                    Prep = J.D(r, "prep", 1), File = J.D(r, "file", 1), Evidence = J.D(r, "evidence", 1), Fatigue = J.D(r, "fatigue", 1), Settle = J.D(r, "settle", 1), Odds = J.D(r, "odds"),
                });
        }

        void ReadEvents(object r)
        {
            if (r == null) return;
            foreach (var n in J.Arr(r, "events") ?? new List<object>())
                Events.Add(new EventDef { Id = J.S(n, "id"), Weight = J.D(n, "weight", 1), MinDay = J.I(n, "min_day", 1), Hours = J.D(n, "hours", 1.5), Minutes = J.I(n, "minutes", 60) });
        }

        void Validate()
        {
            if (Areas.Count == 0) Errors.Add("matters: no areas");
            foreach (var a in Areas)
            {
                if (!Fees.Valid(a.Fee)) Errors.Add("matters: area " + a.Id + " fee " + a.Fee);
                if (a.AfterMin < 2 || a.AfterMax < a.AfterMin) Errors.Add("matters: area " + a.Id + " hearing window");
                if (a.Claims < 1 || a.Claims > 5) Errors.Add("matters: area " + a.Id + " claims");
                if (a.Fee == Fees.Contingency && a.Value <= 0) Errors.Add("matters: area " + a.Id + " value");
                if (a.Fee == Fees.Retainer && a.Retainer <= 0) Errors.Add("matters: area " + a.Id + " retainer");
            }
            foreach (var t in new[] { "S", "M", "L" }) if (!Tiers.ContainsKey(t)) Errors.Add("matters: tier " + t);
            if (Judges.Count == 0) Errors.Add("matters: no judges");
            if (Parties.Count < 8) Errors.Add("matters: parties");
            foreach (var id in StartStaff) if (!Roles.ContainsKey(id)) Errors.Add("balance: start staff " + id);
            if (StartStaff.Count == 0 || StartStaff[0] != "partner") Errors.Add("balance: the founder (a partner) starts the firm");
            foreach (var id in RoleOrder) if (!Roles.ContainsKey(id)) Errors.Add("office: role_order " + id);
            if (Chapters.Count == 0 || Chapters[0].Chapter != 1) Errors.Add("office: chapters");
            foreach (var r in Rooms) if (r.BuildS > 20 * 60) Errors.Add("office: room " + r.Id + " longer than 20 min");
            if (HearingSlots.Length == 0) Errors.Add("balance: hearing slots");
            foreach (int s in HearingSlots) if (s <= DayStart || s >= DayEnd - HearingSessionMin) Errors.Add("balance: hearing slot " + s);
            foreach (var id in new[] { "billing_boost", "case_preview", "offline_x2" })
                if (!Rewards.ContainsKey(id)) Errors.Add("balance: reward " + id);
        }
    }
}
