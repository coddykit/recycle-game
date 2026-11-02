// Semester Rush sim core — engine independent (no UnityEngine). C# 9 / .NET Standard 2.1.
// Typed view of the data files in Assets/Resources/data (balance.json, map.json, research.json, events.json).
// The Unity layer passes a loader (name -> text); tests read the files from disk. Errors are collected, never thrown.
using System;
using System.Collections.Generic;

namespace SemesterRush.Sim
{
    public static class Rooms
    {
        public const string Lecture = "lecture", Lab = "lab";
    }

    public static class Depts
    {
        public const string LabTechs = "lab_techs", Maintenance = "maintenance", Marshals = "marshals", Canteen = "canteen_crew", Registry = "registry";
        /// <summary>The five staff departments next to the professors (the plan's "6 department staff groups").</summary>
        public static readonly string[] All = { LabTechs, Maintenance, Marshals, Canteen, Registry };
    }

    public sealed class BuildingDef
    {
        public string Id, Kind, Room = "";
        public int Seats, Meals, Beds;
        public double Price, Upkeep, BuildMin, Rent, Prestige, Quality;
        public bool Teaching => Kind == "hall" || Kind == "lab";
    }

    public sealed class ZoneDef { public string Id; public int Chapter; public double Prestige, Cost; }
    public sealed class NodeDef { public int Index; public string Id, Zone, Kind, Building = ""; public double X, Y; }
    public sealed class EdgeDef { public int Index, A, B; public double Len; public string Type = "path"; }
    public sealed class FacultyDef { public string Id, Zone; public List<string> Profs = new List<string>(); public int Index; }
    public sealed class CohortDef { public int Index; public string Id, Faculty; public int Size; }
    public sealed class CourseDef { public int Index; public string Id, Cohort, Room; public int PerWeek, CohortIndex; }
    public sealed class DeptDef { public string Id; public double Hire, Wage; public int Max; }
    public sealed class ResearchDef { public string Id, Branch, Zone; public double Cost, Minutes; public List<string> Requires = new List<string>(); }
    public sealed class EventDef { public string Id; public double Weight, Mult, Bonus; public int MinWeek, Days, ClosedMin; public bool Announce, Decision, Exam; }
    public sealed class RewardDef { public string Id; public int PerDay, Count; public long CooldownS; public double Days; }

    public sealed class GameData
    {
        public readonly List<string> Errors = new List<string>();
        public bool Ok => Errors.Count == 0;

        // time
        public int DayStart = 530, ClassMin = 80, BellMin = 10, DaysPerWeek = 5, WeeksPerSemester = 6, FreeMaxSpeed = 2, PaidMaxSpeed = 4;
        public int[] Slots = { 540, 630, 720, 810, 900 };
        public int[] LunchSlots = { 2, 3 };
        public double GameMinPerRealS = 12, BellMinPerRealS = 2.5, AutosaveS = 30;
        // walking
        public double WalkSpeed = 80, PathFactor = 1.1, Congestion = 1.4, Spread = 0.2, LateCredit = 0.4, SnowSpeed = 0.6, BikeSpeed = 1.15,
            SignpostTime = 0.92, ShuttleSpeed = 3, MarshalCap = 0.6, PathCap = 30, GateCap = 20, ShuttleCap = 8, WidenMult = 2, DrillTime = 1.35, OpenDayLoad = 1.4, StaggeredCostDay = 60;
        // start
        public double StartCash = 3000, StartPrestige = 100;
        public List<string> StartZones = new List<string>(), StartProfs = new List<string>();
        public readonly Dictionary<string, int> StartStaff = new Dictionary<string, int>();
        public int GuideDay, GuideSlot;
        public string GuideCourse = "";
        // catalogue
        public readonly Dictionary<string, BuildingDef> Buildings = new Dictionary<string, BuildingDef>();
        public readonly List<FacultyDef> Faculties = new List<FacultyDef>();
        public readonly List<CohortDef> Cohorts = new List<CohortDef>();
        public readonly List<CourseDef> Courses = new List<CourseDef>();
        public readonly Dictionary<string, DeptDef> Staff = new Dictionary<string, DeptDef>();
        public double ProfHire = 800, ProfWage = 320;
        public double[] ProfSkill = { 0.78, 0.86, 0.92 };
        public int ProfMaxPerDay = 3, ProfMaxPerWeek = 10, ProfPerFaculty = 3;
        // economy
        public double ServicesPerStudent = 2, Tuition = 5, PrestigeMultMin = 0.8, PrestigeMultSpan = 0.8, GateCost = 150, WidenCost = 900, CrewMeals = 0.25, FoodTruckMeals = 40,
            HungerMult = 0.85, LabUntechMult = 0.6, SubProfMult = 0.6, SmartBoards = 0.08, LabEquipment = 0.12, FacultyDev = 0.05, TutoringLate = 0.6,
            MaintBuildSpeed = 0.15, DayIncomeFloor = 300, ExamFee = 6, GrantPerStudent = 10, ScholarshipPerStudent = 12, InvigilatorPer = 50,
            UninvigilatedMult = 0.85, DormComfortRent = 1.25, IntakeGrow = 1.2, IntakeSelect = 1.05, IntakeSelectApt = 0.04, IntakeGrowApt = -0.03, RegistryGrow = 0.03;
        public int MaxCohort = 240;
        // prestige
        public double WeeklyGain = 45, GradePar = 0.55, SemesterGain = 70, PrestigeMax = 1000, ClubsPrestige = 3, DormComfortPrestige = 1, GymPrestige = 2;
        public int PrestigeFreeWeeks = 3;
        // events
        public int EventFreeWeeks = 1, DecisionMin = 90;
        public double[] EventsPerDay = { 0, 0.25, 0.35, 0.45, 0.5 };
        // offline
        public double OfflineMinS = 120, OfflineCapH = 4, OfflineCapHPlus = 12, OfflineRate = 0.5, OfflineRatePlus = 0.8, OfflineWeeksPerHour = 0.5;
        public readonly Dictionary<string, RewardDef> Rewards = new Dictionary<string, RewardDef>();
        public double RewardSingleMaxDays = 0.2;

        // map
        public readonly List<ZoneDef> Zones = new List<ZoneDef>();
        public readonly List<NodeDef> Nodes = new List<NodeDef>();
        public readonly List<EdgeDef> Edges = new List<EdgeDef>();
        public readonly Dictionary<string, int> NodeIndex = new Dictionary<string, int>();
        public int EntranceNode = -1;

        public readonly List<ResearchDef> Research = new List<ResearchDef>();
        public readonly Dictionary<string, ResearchDef> ResearchById = new Dictionary<string, ResearchDef>();
        public readonly List<EventDef> Events = new List<EventDef>();

        public ZoneDef Zone(string id) => Zones.Find(z => z.Id == id);
        public int Node(string id) => NodeIndex.TryGetValue(id ?? "", out int i) ? i : -1;
        public FacultyDef Faculty(string id) => Faculties.Find(f => f.Id == id);
        public int CohortIndex(string id) => Cohorts.FindIndex(c => c.Id == id);
        public int CourseIndex(string id) => Courses.FindIndex(c => c.Id == id);
        public BuildingDef Building(string id) => Buildings.TryGetValue(id ?? "", out var b) ? b : null;
        public int SlotCount => Slots.Length;
        public int DayEnd => Slots[Slots.Length - 1] + ClassMin;
        public bool IsLunchSlot(int slot) => Array.IndexOf(LunchSlots, slot) >= 0;
        public string FacultyOfCourse(CourseDef c) => Cohorts[c.CohortIndex].Faculty;

        public static GameData FromTexts(Func<string, string> load)
        {
            var gd = new GameData();
            gd.ReadBalance(Parse(gd, load, "balance"));
            gd.ReadMap(Parse(gd, load, "map"));
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

        static int[] Ints(object o, string key, int[] def)
        {
            var a = J.IntArr(o, key);
            return a != null && a.Length > 0 ? a : def;
        }

        void ReadBalance(object b)
        {
            if (b == null) return;
            var t = J.Obj(b, "time");
            DayStart = J.I(t, "day_start", 530); Slots = Ints(t, "slots", Slots); ClassMin = J.I(t, "class_min", 80); BellMin = J.I(t, "bell_min", 10);
            LunchSlots = Ints(t, "lunch_slots", LunchSlots);
            GameMinPerRealS = J.D(t, "game_min_per_real_s", 12); BellMinPerRealS = J.D(t, "bell_min_per_real_s", 2.5);
            DaysPerWeek = J.I(t, "days_per_week", 5); WeeksPerSemester = J.I(t, "weeks_per_semester", 6);
            AutosaveS = J.D(t, "autosave_s", 30); FreeMaxSpeed = J.I(t, "free_max_speed", 2); PaidMaxSpeed = J.I(t, "paid_max_speed", 4);

            var w = J.Obj(b, "walk");
            WalkSpeed = J.D(w, "speed_m_per_min", 80); PathFactor = J.D(w, "path_factor", 1.1); Congestion = J.D(w, "congestion", 1.4);
            Spread = J.D(w, "spread", 0.2); LateCredit = J.D(w, "late_credit", 0.4); SnowSpeed = J.D(w, "snow_speed", 0.6);
            BikeSpeed = J.D(w, "bike_lanes_speed", 1.15); SignpostTime = J.D(w, "signposts_time", 0.92); ShuttleSpeed = J.D(w, "shuttle_speed", 3);
            MarshalCap = J.D(w, "marshal_cap", 0.6); PathCap = J.D(w, "path_cap", 30); GateCap = J.D(w, "gate_cap", 20);
            ShuttleCap = J.D(w, "shuttle_cap", 8); WidenMult = J.D(w, "widen_mult", 2); DrillTime = J.D(w, "drill_time", 1.35); OpenDayLoad = J.D(w, "open_day_load", 1.4);
            StaggeredCostDay = J.D(w, "staggered_cost_day", 60);

            var s = J.Obj(b, "start");
            StartCash = J.D(s, "cash", 3000); StartPrestige = J.D(s, "prestige", 100);
            StartZones = J.StrList(s, "zones") ?? new List<string>();
            StartProfs = J.StrList(s, "professors") ?? new List<string>();
            if (J.Obj(s, "staff") is JObj st) foreach (var kv in st) StartStaff[kv.Key] = J.I(kv.Value);
            var gc = J.IntArr(s, "guide_cell");
            if (gc != null && gc.Length == 2) { GuideDay = gc[0]; GuideSlot = gc[1]; }
            GuideCourse = J.S(s, "guide_course");

            if (J.Obj(b, "buildings") is JObj bs)
                foreach (var kv in bs)
                    Buildings[kv.Key] = new BuildingDef
                    {
                        Id = kv.Key, Kind = J.S(kv.Value, "kind"), Room = J.S(kv.Value, "room"), Seats = J.I(kv.Value, "seats"), Meals = J.I(kv.Value, "meals"),
                        Beds = J.I(kv.Value, "beds"), Price = J.D(kv.Value, "price"), Upkeep = J.D(kv.Value, "upkeep"), BuildMin = J.D(kv.Value, "build_min"),
                        Rent = J.D(kv.Value, "rent"), Prestige = J.D(kv.Value, "prestige"), Quality = J.D(kv.Value, "quality"),
                    };
            if (J.Obj(b, "faculties") is JObj fs)
                foreach (var kv in fs)
                    Faculties.Add(new FacultyDef { Id = kv.Key, Zone = J.S(kv.Value, "zone"), Profs = J.StrList(kv.Value, "profs") ?? new List<string>(), Index = Faculties.Count });
            foreach (var c in J.Arr(b, "cohorts") ?? new List<object>())
                Cohorts.Add(new CohortDef { Index = Cohorts.Count, Id = J.S(c, "id"), Faculty = J.S(c, "faculty"), Size = J.I(c, "size", 40) });
            foreach (var c in J.Arr(b, "courses") ?? new List<object>())
            {
                var cd = new CourseDef { Index = Courses.Count, Id = J.S(c, "id"), Cohort = J.S(c, "cohort"), Room = J.S(c, "room", Rooms.Lecture), PerWeek = J.I(c, "per_week", 2) };
                cd.CohortIndex = CohortIndex(cd.Cohort);
                Courses.Add(cd);
            }
            var pr = J.Obj(b, "professor");
            ProfHire = J.D(pr, "hire", 800); ProfWage = J.D(pr, "wage", 320); ProfSkill = Dbl(pr, "skill", ProfSkill);
            ProfMaxPerDay = J.I(pr, "max_per_day", 3); ProfMaxPerWeek = J.I(pr, "max_per_week", 10); ProfPerFaculty = J.I(pr, "per_faculty", 3);
            if (J.Obj(b, "staff") is JObj sf)
                foreach (var kv in sf)
                    Staff[kv.Key] = new DeptDef { Id = kv.Key, Hire = J.D(kv.Value, "hire"), Wage = J.D(kv.Value, "wage"), Max = J.I(kv.Value, "max", 4) };

            var e = J.Obj(b, "economy");
            Tuition = J.D(e, "tuition", 5); ServicesPerStudent = J.D(e, "services_per_student", 2); PrestigeMultMin = J.D(e, "prestige_mult_min", 0.8); PrestigeMultSpan = J.D(e, "prestige_mult_span", 0.8);
            GateCost = J.D(e, "gate_cost", 150); WidenCost = J.D(e, "widen_cost", 900); CrewMeals = J.D(e, "crew_meals", 0.25);
            FoodTruckMeals = J.D(e, "food_trucks_meals", 40); HungerMult = J.D(e, "hunger_mult", 0.85); LabUntechMult = J.D(e, "lab_untech_mult", 0.6);
            SubProfMult = J.D(e, "sub_prof_mult", 0.6); SmartBoards = J.D(e, "smart_boards", 0.08); LabEquipment = J.D(e, "lab_equipment", 0.12);
            FacultyDev = J.D(e, "faculty_dev", 0.05); TutoringLate = J.D(e, "tutoring_late", 0.6); MaintBuildSpeed = J.D(e, "maint_build_speed", 0.15);
            DayIncomeFloor = J.D(e, "day_income_floor", 300); ExamFee = J.D(e, "exam_fee", 6); GrantPerStudent = J.D(e, "grant_per_student", 10);
            ScholarshipPerStudent = J.D(e, "scholarship_per_student", 12); InvigilatorPer = J.D(e, "invigilator_per", 50);
            UninvigilatedMult = J.D(e, "uninvigilated_mult", 0.85); DormComfortRent = J.D(e, "dorm_comfort_rent", 1.25); MaxCohort = J.I(e, "max_cohort", 240);
            IntakeGrow = J.D(e, "intake_grow", 1.2); IntakeSelect = J.D(e, "intake_select", 1.05); IntakeSelectApt = J.D(e, "intake_select_apt", 0.04);
            IntakeGrowApt = J.D(e, "intake_grow_apt", -0.03); RegistryGrow = J.D(e, "registry_grow", 0.03);

            var p = J.Obj(b, "prestige");
            WeeklyGain = J.D(p, "weekly_gain", 45); GradePar = J.D(p, "grade_par", 0.55); SemesterGain = J.D(p, "semester_gain", 70);
            PrestigeMax = J.D(p, "max", 1000); PrestigeFreeWeeks = J.I(p, "free_weeks", 3); ClubsPrestige = J.D(p, "clubs", 3);
            DormComfortPrestige = J.D(p, "dorm_comfort", 1); GymPrestige = J.D(p, "gym", 2);

            var ev = J.Obj(b, "events");
            EventFreeWeeks = J.I(ev, "free_weeks", 1); EventsPerDay = Dbl(ev, "per_day", EventsPerDay); DecisionMin = J.I(ev, "decision_min", 90);

            var of = J.Obj(b, "offline");
            OfflineMinS = J.D(of, "min_s", 120); OfflineCapH = J.D(of, "cap_h", 4); OfflineCapHPlus = J.D(of, "cap_h_plus", 12);
            OfflineRate = J.D(of, "rate", 0.5); OfflineRatePlus = J.D(of, "rate_plus", 0.8); OfflineWeeksPerHour = J.D(of, "weeks_per_hour", 0.5);

            if (J.Obj(b, "rewards") is JObj rw)
                foreach (var kv in rw)
                {
                    if (!(kv.Value is JObj)) { if (kv.Key == "single_max_days") RewardSingleMaxDays = J.D(kv.Value, 0.2); continue; }
                    Rewards[kv.Key] = new RewardDef
                    {
                        Id = kv.Key, PerDay = J.I(kv.Value, "per_day", 1), CooldownS = J.L(kv.Value, "cooldown_s"), Days = J.D(kv.Value, "days"), Count = J.I(kv.Value, "count"),
                    };
                }
        }

        void ReadMap(object m)
        {
            if (m == null) return;
            foreach (var z in J.Arr(m, "zones") ?? new List<object>())
                Zones.Add(new ZoneDef { Id = J.S(z, "id"), Chapter = J.I(z, "chapter", 1), Prestige = J.D(z, "prestige"), Cost = J.D(z, "cost") });
            foreach (var n in J.Arr(m, "nodes") ?? new List<object>())
            {
                var nd = new NodeDef
                {
                    Index = Nodes.Count, Id = J.S(n, "id"), Zone = J.S(n, "zone"), Kind = J.S(n, "kind"), Building = J.S(n, "building"), X = J.D(n, "x"), Y = J.D(n, "y"),
                };
                if (NodeIndex.ContainsKey(nd.Id)) { Errors.Add("map: duplicate node " + nd.Id); continue; }
                NodeIndex[nd.Id] = nd.Index;
                Nodes.Add(nd);
                if (nd.Kind == "entrance") EntranceNode = nd.Index;
            }
            foreach (var e in J.Arr(m, "edges") ?? new List<object>())
            {
                if (!(e is List<object> l) || l.Count < 2) { Errors.Add("map: bad edge"); continue; }
                int a = Node(J.Str(l[0])), b = Node(J.Str(l[1]));
                if (a < 0 || b < 0) { Errors.Add("map: edge to unknown node " + J.Str(l[0]) + "-" + J.Str(l[1])); continue; }
                double dx = Nodes[a].X - Nodes[b].X, dy = Nodes[a].Y - Nodes[b].Y;
                string type = l.Count > 2 ? J.Str(l[2]) : "path";
                Edges.Add(new EdgeDef { Index = Edges.Count, A = a, B = b, Len = Math.Sqrt(dx * dx + dy * dy) * 100.0 * PathFactor, Type = type });
            }
        }

        void ReadResearch(object r)
        {
            if (r == null) return;
            foreach (var n in J.Arr(r, "nodes") ?? new List<object>())
            {
                var d = new ResearchDef { Id = J.S(n, "id"), Branch = J.S(n, "branch"), Cost = J.D(n, "cost"), Minutes = J.D(n, "min"), Zone = J.S(n, "zone"), Requires = J.StrList(n, "requires") ?? new List<string>() };
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
                    Id = J.S(n, "id"), Weight = J.D(n, "weight", 1), MinWeek = J.I(n, "min_week", 2), Days = J.I(n, "days", 1), Mult = J.D(n, "mult", 1),
                    Bonus = J.D(n, "bonus"), ClosedMin = J.I(n, "closed_min", 300), Announce = J.B(n, "announce"), Decision = J.B(n, "decision"), Exam = J.B(n, "exam"),
                });
        }

        void Validate()
        {
            if (EntranceNode < 0) Errors.Add("map: no entrance");
            if (Slots.Length < 3) Errors.Add("balance: slots");
            for (int i = 1; i < Slots.Length; i++) if (Slots[i] - Slots[i - 1] < ClassMin + BellMin) Errors.Add("balance: slot " + i + " overlaps");
            foreach (int l in LunchSlots) if (l < 0 || l >= Slots.Length) Errors.Add("balance: lunch slot " + l);
            foreach (var z in StartZones) if (Zone(z) == null) Errors.Add("balance: start zone " + z);
            foreach (var f in StartProfs) if (Faculty(f) == null) Errors.Add("balance: start professor " + f);
            foreach (var d in Depts.All) if (!Staff.ContainsKey(d)) Errors.Add("balance: staff " + d);
            foreach (var n in Nodes)
            {
                if (Zone(n.Zone) == null) Errors.Add("map: node " + n.Id + " unknown zone");
                if ((n.Kind == "site" || n.Kind == "building") && Building(n.Building) == null) Errors.Add("map: node " + n.Id + " building " + n.Building);
            }
            foreach (var e in Edges) if (e.Type != "path" && e.Type != "gate" && e.Type != "shuttle") Errors.Add("map: edge type " + e.Type);
            foreach (var f in Faculties) { if (Zone(f.Zone) == null) Errors.Add("faculty " + f.Id + " zone"); if (f.Profs.Count < ProfPerFaculty) Errors.Add("faculty " + f.Id + " names"); }
            foreach (var c in Cohorts) if (Faculty(c.Faculty) == null) Errors.Add("cohort " + c.Id + " faculty");
            foreach (var c in Courses)
            {
                if (c.CohortIndex < 0) Errors.Add("course " + c.Id + " cohort");
                if (c.Room != Rooms.Lecture && c.Room != Rooms.Lab) Errors.Add("course " + c.Id + " room");
            }
            if (CourseIndex(GuideCourse) < 0) Errors.Add("balance: guide course");
            foreach (var r in Research)
            {
                foreach (var q in r.Requires) if (!ResearchById.ContainsKey(q)) Errors.Add("research: " + r.Id + " requires unknown " + q);
                if (r.Minutes > 20) Errors.Add("research: " + r.Id + " longer than 20 min");
                if (!string.IsNullOrEmpty(r.Zone) && Zone(r.Zone) == null) Errors.Add("research: " + r.Id + " zone");
            }
            foreach (var id in new[] { "offline_x2", "extra_marshals", "daily_bonus", "city_help" })
                if (!Rewards.ContainsKey(id)) Errors.Add("balance: reward " + id);
        }
    }
}
