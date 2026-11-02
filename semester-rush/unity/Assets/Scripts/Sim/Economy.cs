// Semester Rush sim core — the week end, the semester close, the economy, purchases (buildings, gates, wider paths,
// professors, staff, zones), upgrades, offline pay and the sim side of rewarded placements (caps + cooldowns persisted in
// the career; the kit's RewardedController enforces its own too).
using System;
using System.Collections.Generic;

namespace SemesterRush.Sim
{
    public sealed class OfflineSummary { public double Seconds, Hours, Amount; public bool Capped; }
    public sealed class RewardOfferInfo { public bool Ok; public double Amount; public string Reason = ""; }
    public sealed class RewardResult { public bool Ok; public double Amount; public string Reason = ""; }
    public sealed class BuyInfo { public bool Ok; public double Cost; public string Reason = ""; }

    public sealed partial class Simulation
    {
        // ------------------------------------------------------------------------------------------------ week cycle

        /// <summary>Weekly grade of a cohort: session points over the sessions its courses need (missing classes count 0).</summary>
        public double CohortGrade(int cohort)
        {
            double sum = 0; int need = 0;
            for (int ci = 0; ci < Gd.Courses.Count; ci++)
            {
                if (Gd.Courses[ci].CohortIndex != cohort) continue;
                sum += S.WeekScore[ci];
                need += Needed(ci);
            }
            return need <= 0 ? 0 : Math.Min(1, sum / need);
        }

        void EndWeek()
        {
            var w = S.ThisWeek;
            w.Week = S.Week;
            w.Exam = ExamWeek;
            CollectTill();
            // grades
            double gsum = 0, wsum = 0;
            w.Cohort = new double[Gd.Cohorts.Count];
            for (int c = 0; c < Gd.Cohorts.Count; c++)
            {
                if (!CohortOn(c)) { w.Cohort[c] = -1; continue; }
                double g = CohortGrade(c);
                w.Cohort[c] = g;
                gsum += g * S.Cohorts[c].Size;
                wsum += S.Cohorts[c].Size;
                if (!w.Exam) { S.Cohorts[c].SemSum += g; S.Cohorts[c].SemWeeks++; }
            }
            w.Grade = wsum > 0 ? gsum / wsum : 0;
            w.Required = RequiredSessions;
            // money
            foreach (var p in S.Profs) w.Wages += Gd.ProfWage;
            foreach (var dept in Depts.All) w.Wages += Staff(dept) * Gd.Staff[dept].Wage;
            foreach (var b in S.Buildings) if (b.Built) w.Upkeep += DefOf(b)?.Upkeep ?? 0;
            w.Upkeep += TotalStudents * Gd.ServicesPerStudent;
            int residents = Math.Min(Beds, TotalStudents);
            w.Rent = residents * (Gd.Building("dorm")?.Rent ?? 0) * (Has("dorm_comfort") ? Gd.DormComfortRent : 1.0);
            if (S.Staggered && Has("staggered_bells")) w.Overtime = Gd.StaggeredCostDay * Gd.DaysPerWeek;
            S.Cash += w.Rent - w.Wages - w.Upkeep - w.Overtime;
            // prestige
            double d = Gd.WeeklyGain * (w.Grade - Gd.GradePar);
            if (BuiltOf("library").Count > 0) d += Gd.Building("library").Prestige;
            if (Has("clubs")) d += Gd.ClubsPrestige;
            if (Has("dorm_comfort") && Beds > 0) d += Gd.DormComfortPrestige;
            if (Has("gym")) d += Gd.GymPrestige;
            if (w.Exam) CloseSemester(w, ref d);
            if (S.Tutorial || S.Week <= Gd.PrestigeFreeWeeks) d = Math.Max(0, d);
            double before = S.Prestige;
            S.Prestige = Math.Max(0, Math.Min(Gd.PrestigeMax, S.Prestige + d));
            w.PrestigeDelta = S.Prestige - before;
            if (w.Grade > S.BestGrade) S.BestGrade = w.Grade;
            if (w.Trips > 0 && w.Late == 0) S.ZeroLateWeeks++;
            S.History.Add(w);
            while (S.History.Count > 12) S.History.RemoveAt(0);
            S.Phase = Phases.Report;
            Emit("week_done", S.Week, w.Late, w.Grade, w.Exam ? "exam" : "");
        }

        void CloseSemester(WeekStats w, ref double prestigeDelta)
        {
            double sum = 0, n = 0, grants = 0;
            for (int c = 0; c < Gd.Cohorts.Count; c++)
            {
                if (!CohortOn(c)) continue;
                var cs = S.Cohorts[c];
                double weekly = cs.SemWeeks > 0 ? cs.SemSum / cs.SemWeeks : 0;
                double exam = CohortGrade(c);
                double sem = 0.6 * weekly + 0.4 * exam;
                sum += sem * cs.Size; n += cs.Size;
                grants += cs.Size * Gd.GrantPerStudent * sem * (Has("grant_office") ? 1.5 : 1.0);
            }
            w.SemesterGrade = n > 0 ? sum / n : 0;
            w.Grants = Math.Round(grants);
            S.Cash += w.Grants;
            prestigeDelta += Gd.SemesterGain * (w.SemesterGrade - Gd.GradePar);
        }

        /// <summary>Semester close: the dean's intake decision for next semester (keep / grow / select).</summary>
        public string SetIntake(string choice)
        {
            if (!Intakes.Valid(choice)) return "invalid";
            S.Intake = choice;
            return "";
        }

        /// <summary>Report -> next Sunday's plan.</summary>
        public bool NextWeek()
        {
            if (S.Phase != Phases.Report) return false;
            bool semesterEnd = ExamWeek;
            // poached professors leave; their courses wait for a new name
            for (int p = S.Profs.Count - 1; p >= 0; p--)
            {
                if (!S.Profs[p].Leaving) continue;
                S.Profs.RemoveAt(p);
                for (int c = 0; c < S.CourseProf.Count; c++)
                {
                    if (S.CourseProf[c] == p) S.CourseProf[c] = -1;
                    else if (S.CourseProf[c] > p) S.CourseProf[c]--;
                }
            }
            if (semesterEnd)
            {
                ApplyIntake();
                S.Exams.Clear();
                S.ExamKit = false;
                foreach (var c in S.Cohorts) { c.SemSum = 0; c.SemWeeks = 0; }
            }
            S.Week++;
            S.Day = 0;
            S.Minute = Gd.DayStart;
            for (int c = 0; c < S.WeekScore.Count; c++) { S.WeekScore[c] = 0; S.WeekHeld[c] = 0; }
            S.ThisWeek = NewWeekStats();
            S.Pending = null;
            S.Phase = Phases.Plan;
            S.Paused = false;
            bell = null;
            if (ExamWeek && S.Exams.Count == 0) AutoFill(true);   // exam week: the registry drafts the exam plan
            Emit("week_start", S.Week, ExamWeek ? 1 : 0);
            return true;
        }

        void ApplyIntake()
        {
            string ch = S.Intake;
            double grow = ch == Intakes.Grow ? Gd.IntakeGrow + Gd.RegistryGrow * Staff(Depts.Registry) : ch == Intakes.Select ? Gd.IntakeSelect : 1.0;
            double apt = ch == Intakes.Grow ? Gd.IntakeGrowApt : ch == Intakes.Select ? Gd.IntakeSelectApt : 0;
            double scholarships = 0;
            for (int c = 0; c < S.Cohorts.Count; c++)
            {
                if (!CohortOn(c)) continue;
                var cs = S.Cohorts[c];
                cs.Size = Math.Max(10, Math.Min(Gd.MaxCohort, (int)Math.Round(cs.Size * grow)));
                cs.Aptitude = Math.Max(0.8, Math.Min(1.15, cs.Aptitude + apt + (Has("scholarships") && ch == Intakes.Select ? 0.02 : 0)));
                if (ch == Intakes.Select) scholarships += cs.Size * Gd.ScholarshipPerStudent;
            }
            S.Cash -= scholarships;
            S.ThisWeek.Scholarships += scholarships;
            Emit("intake", -1, -1, grow, ch);
            S.Intake = Intakes.Keep;
        }

        // ------------------------------------------------------------------------------------------------ money

        /// <summary>The bursar's till: tuition of the classes held, banked by a tap (and at every week end).</summary>
        public double CollectTill()
        {
            double t = Math.Floor(S.Till);
            if (t <= 0) return 0;
            S.Till -= t;
            S.Cash += t;
            Emit("till", -1, -1, t);
            return t;
        }

        /// <summary>Gross income of a usual day (1/5 of the average of the last 3 weeks, floor) — the unit of packs and rewards.</summary>
        public double DayIncome()
        {
            double sum = 0; int n = 0;
            for (int i = S.History.Count - 1; i >= 0 && n < 3; i--, n++) sum += S.History[i].Income;
            double avg = n > 0 ? sum / n / Math.Max(1, Gd.DaysPerWeek) : 0;
            return Math.Max(Gd.DayIncomeFloor, avg);
        }

        public double AvgNet(int weeks = 3)
        {
            double sum = 0; int n = 0;
            for (int i = S.History.Count - 1; i >= 0 && n < weeks; i--, n++) sum += S.History[i].Net;
            return n > 0 ? sum / n : 0;
        }

        /// <summary>Consumable cash pack: days × the usual day income. Returns the amount.</summary>
        public double GrantCash(double days)
        {
            double a = Math.Round(Math.Max(0, days) * DayIncome());
            S.Cash += a;
            Emit("cash_pack", -1, -1, a);
            return a;
        }

        // ------------------------------------------------------------------------------------------------ purchases

        /// <summary>Price + whether it can be bought now. Ids: build:&lt;site node&gt;, gate:&lt;edge&gt;, widen:&lt;edge&gt;,
        /// professor:&lt;faculty&gt;, staff:&lt;dept&gt;, zone:&lt;id&gt;.</summary>
        public BuyInfo CanBuy(string id)
        {
            var r = new BuyInfo();
            if (string.IsNullOrEmpty(id)) { r.Reason = "invalid"; return r; }
            string kind = id, arg = "";
            int c = id.IndexOf(':');
            if (c >= 0) { kind = id.Substring(0, c); arg = id.Substring(c + 1); }
            switch (kind)
            {
                case "build":
                {
                    int node = Gd.Node(arg);
                    if (node < 0 || Gd.Nodes[node].Kind != "site") { r.Reason = "invalid"; return r; }
                    if (BuildingAt(node) != null) { r.Reason = "owned"; return r; }
                    if (!Unlocked(Gd.Nodes[node].Zone)) { r.Reason = "locked"; return r; }
                    r.Cost = Gd.Building(Gd.Nodes[node].Building).Price;
                    break;
                }
                case "gate":
                case "widen":
                {
                    if (!int.TryParse(arg, out int e) || e < 0 || e >= Gd.Edges.Count) { r.Reason = "invalid"; return r; }
                    var ed = Gd.Edges[e];
                    if (!Unlocked(Gd.Nodes[ed.A].Zone) || !Unlocked(Gd.Nodes[ed.B].Zone)) { r.Reason = "locked"; return r; }
                    if (kind == "gate")
                    {
                        if (ed.Type != "gate") { r.Reason = "invalid"; return r; }
                        if (S.OpenGates.Contains(e)) { r.Reason = "owned"; return r; }
                        r.Cost = Gd.GateCost * EdgeChapter(ed);
                    }
                    else
                    {
                        if (ed.Type != "path") { r.Reason = "invalid"; return r; }
                        if (S.Widened.Contains(e)) { r.Reason = "owned"; return r; }
                        if (!Has("wide_paths")) { r.Reason = "research"; return r; }
                        r.Cost = Gd.WidenCost;
                    }
                    break;
                }
                case "professor":
                {
                    var f = Gd.Faculty(arg);
                    if (f == null) { r.Reason = "invalid"; return r; }
                    if (!Unlocked(f.Zone)) { r.Reason = "locked"; return r; }
                    if (S.Profs.FindAll(p => p.Faculty == f.Index).Count >= Gd.ProfPerFaculty) { r.Reason = "max"; return r; }
                    r.Cost = Gd.ProfHire;
                    break;
                }
                case "staff":
                {
                    if (!Gd.Staff.TryGetValue(arg, out var dd)) { r.Reason = "invalid"; return r; }
                    if (Staff(arg) >= dd.Max) { r.Reason = "max"; return r; }
                    r.Cost = dd.Hire;
                    break;
                }
                case "zone":
                {
                    var z = Gd.Zone(arg);
                    if (z == null) { r.Reason = "invalid"; return r; }
                    if (Unlocked(arg)) { r.Reason = "owned"; return r; }
                    if (z.Chapter > Chapter + 1) { r.Reason = "locked"; return r; }
                    if (S.Prestige < z.Prestige) { r.Reason = "prestige"; return r; }
                    r.Cost = z.Cost;
                    break;
                }
                default: r.Reason = "invalid"; return r;
            }
            if (S.Cash < r.Cost) { r.Reason = "cash"; return r; }
            r.Ok = true;
            return r;
        }

        int EdgeChapter(EdgeDef e) => Math.Max(Gd.Zone(Gd.Nodes[e.A].Zone)?.Chapter ?? 1, Gd.Zone(Gd.Nodes[e.B].Zone)?.Chapter ?? 1);

        /// <summary>Buys it. "" = ok, else the CanBuy reason.</summary>
        public string Buy(string id)
        {
            var info = CanBuy(id);
            if (!info.Ok) return info.Reason;
            S.Cash -= info.Cost;
            string kind = id, arg = "";
            int c = id.IndexOf(':');
            if (c >= 0) { kind = id.Substring(0, c); arg = id.Substring(c + 1); }
            switch (kind)
            {
                case "build":
                {
                    int node = Gd.Node(arg);
                    var def = Gd.Building(Gd.Nodes[node].Building);
                    S.Buildings.Add(new BuildingState { Node = node, Def = def.Id, BuildLeft = Math.Max(1, def.BuildMin) });
                    Emit("build_start", node, -1, def.BuildMin, def.Id);
                    break;
                }
                case "gate": S.OpenGates.Add(int.Parse(arg)); RebuildGraph(); Rewalk(); Emit("gate", int.Parse(arg)); break;
                case "widen": S.Widened.Add(int.Parse(arg)); Rewalk(); break;
                case "professor": { int p = AddProf(Gd.Faculty(arg).Index, true); Emit("hired", p, -1, 0, arg); break; }
                case "staff": S.Staff[arg] = Staff(arg) + 1; if (arg == Depts.Marshals) Rewalk(); break;
                case "zone":
                    S.Zones.Add(arg);
                    foreach (var co in Gd.Cohorts) if (Gd.Faculty(co.Faculty).Zone == arg) Enroll(co.Index);
                    RebuildGraph();
                    Emit("zone", -1, -1, 0, arg);
                    break;
            }
            Emit("bought", -1, -1, info.Cost, id);
            return "";
        }

        /// <summary>Lets one member of a department go (no refund). "" = ok.</summary>
        public string Dismiss(string dept)
        {
            if (!Gd.Staff.ContainsKey(dept ?? "")) return "invalid";
            if (Staff(dept) <= 0) return "none";
            S.Staff[dept] = Staff(dept) - 1;
            Emit("fired", -1, -1, 0, dept);
            return "";
        }

        /// <summary>The staggered-bells policy (needs the upgrade; overtime each day it is on).</summary>
        public string SetStaggered(bool on)
        {
            if (on && !Has("staggered_bells")) return "research";
            S.Staggered = on;
            Rewalk();
            return "";
        }

        // ------------------------------------------------------------------------------------------------ research

        public string CanResearch(string id)
        {
            if (!Gd.ResearchById.TryGetValue(id ?? "", out var r)) return "invalid";
            if (Has(id)) return "owned";
            if (S.Researching != "") return S.Researching == id ? "running" : "busy";
            foreach (var q in r.Requires) if (!Has(q)) return "requires";
            if (!string.IsNullOrEmpty(r.Zone) && !Unlocked(r.Zone)) return "locked";
            if (S.Cash < r.Cost) return "cash";
            return "";
        }

        public string StartResearch(string id)
        {
            string why = CanResearch(id);
            if (why != "") return why;
            var r = Gd.ResearchById[id];
            S.Cash -= r.Cost;
            S.Researching = id;
            S.ResearchLeftS = r.Minutes * 60.0;
            Emit("research_start", -1, -1, r.Cost, id);
            return "";
        }

        /// <summary>Real seconds pass for the research timer only (balance bot / tests that advance game minutes directly).</summary>
        public void AdvanceResearch(double realS) { if (realS > 0) TickResearch(realS); }

        void TickResearch(double realS)
        {
            if (S.Researching == "") return;
            S.ResearchLeftS -= realS;
            if (S.ResearchLeftS > 0) return;
            string id = S.Researching;
            S.Researching = "";
            S.ResearchLeftS = 0;
            if (!S.Research.Contains(id)) S.Research.Add(id);
            if (id == "shuttle") RebuildGraph();
            Emit("research_done", -1, -1, 0, id);
        }

        // ------------------------------------------------------------------------------------------------ offline

        /// <summary>The time a save was written (unix seconds).</summary>
        public void Touch(long now) { S.LastSeen = now; }

        /// <summary>Share of this week's required classes the timetable holds (the "policy" that runs while away).</summary>
        public double PlanCompleteness { get { int req = RequiredSessions; return req <= 0 ? 0 : (double)(req - Missing) / req; } }

        /// <summary>Back from away ("break between terms"): upgrades catch up; the campus earns by its timetable (rate 50 %,
        /// cap 4 h; Offline Plus 80 % / 12 h). Null when nothing was earned.</summary>
        public OfflineSummary Offline(long now)
        {
            if (S.LastSeen <= 0) { S.LastSeen = now; return null; }
            double away = now - S.LastSeen;
            S.LastSeen = now;
            if (away < Gd.OfflineMinS) return null;
            TickResearch(away);
            bool plus = Perk("offline_plus");
            double capH = plus ? Gd.OfflineCapHPlus : Gd.OfflineCapH;
            double hours = Math.Min(away / 3600.0, capH);
            double share = Math.Max(0.25, PlanCompleteness);
            double amount = Math.Round(Math.Max(0, AvgNet()) * hours * Gd.OfflineWeeksPerHour * (plus ? Gd.OfflineRatePlus : Gd.OfflineRate) * share);
            if (amount <= 0) return null;
            S.Cash += amount;
            S.OfflinePending = amount;
            return new OfflineSummary { Seconds = away, Hours = hours, Amount = amount, Capped = away / 3600.0 > capH };
        }

        // ------------------------------------------------------------------------------------------------ rewards (sim side)

        public static readonly string[] RewardPlacements = { "offline_x2", "extra_marshals", "daily_bonus", "city_help" };
        public static bool RewardIsCash(string placement) => placement == "offline_x2" || placement == "daily_bonus";

        public RewardOfferInfo RewardOffer(string placement, long now)
        {
            var o = new RewardOfferInfo();
            if (!Gd.Rewards.TryGetValue(placement ?? "", out var def)) { o.Reason = "invalid"; return o; }
            if (S.Tutorial) { o.Reason = "tutorial"; return o; }
            long day = now / 86400;
            if (S.Rewards.TryGetValue(placement, out var u))
            {
                if (u.UtcDay == day && u.Count >= def.PerDay) { o.Reason = "cap"; return o; }
                if (def.CooldownS > 0 && now - u.LastAt < def.CooldownS) { o.Reason = "cooldown"; return o; }
            }
            double single = Gd.RewardSingleMaxDays * DayIncome();
            switch (placement)
            {
                case "offline_x2":
                    if (S.OfflinePending <= 0) { o.Reason = "context"; return o; }
                    o.Amount = Math.Round(Math.Min(S.OfflinePending, single)); break;
                case "extra_marshals":
                    if (S.Phase == Phases.Report || S.ExtraMarshals > 0) { o.Reason = "context"; return o; }
                    o.Amount = Math.Max(1, def.Count);
                    break;
                case "city_help":
                    if (HelpableCrisis() == "") { o.Reason = "context"; return o; }
                    break;
                case "daily_bonus":
                    o.Amount = Math.Round(Math.Min(def.Days * DayIncome(), single)); break;
            }
            o.Ok = true;
            return o;
        }

        public RewardResult GrantReward(string placement, long now)
        {
            var r = new RewardResult();
            var o = RewardOffer(placement, now);
            if (!o.Ok) { r.Reason = o.Reason; return r; }
            switch (placement)
            {
                case "offline_x2": S.Cash += o.Amount; S.OfflinePending = 0; r.Amount = o.Amount; break;
                case "daily_bonus": S.Cash += o.Amount; r.Amount = o.Amount; break;
                case "extra_marshals": S.ExtraMarshals = (int)o.Amount; r.Amount = o.Amount; Rewalk(); break;
                case "city_help": r.Amount = 0; EndCrisis(HelpableCrisis()); break;
            }
            long day = now / 86400;
            if (!S.Rewards.TryGetValue(placement, out var u) || u.UtcDay != day) u = new RewardUse { UtcDay = day };
            u.Count++;
            u.LastAt = now;
            S.Rewards[placement] = u;
            r.Ok = true;
            Emit("reward", -1, -1, r.Amount, placement);
            return r;
        }

        /// <summary>The guide finished or was skipped: crises may start (after the free week).</summary>
        public void EndTutorial() { S.Tutorial = false; }
    }
}
