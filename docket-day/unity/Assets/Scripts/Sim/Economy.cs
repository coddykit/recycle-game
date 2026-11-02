// Docket Day sim core — the day cycle (plan -> live -> report), the billing run, salaries + rent, stars, hiring, rooms
// (minutes-long builds on real time, never paid builders), chapters (Standing + closed matters + a move cost), standing
// instructions (auto-intake rules), offline pay and the sim side of rewarded placements (caps + cooldowns persisted in
// the career; the kit's RewardedController enforces its own too). No vault, no stored-cash cap: money is never capped.
using System;
using System.Collections.Generic;

namespace DocketDay.Sim
{
    public sealed class OfflineSummary { public double Seconds, Hours, Amount; public bool Capped; }
    public sealed class RewardOfferInfo { public bool Ok; public double Amount; public string Reason = ""; }
    public sealed class RewardResult { public bool Ok; public double Amount; public string Reason = ""; }
    public sealed class BuyInfo { public bool Ok; public double Cost; public string Reason = ""; }

    public sealed partial class Simulation
    {
        // ------------------------------------------------------------------------------------------------ day cycle

        /// <summary>Plan -> live (09:00).</summary>
        public bool StartDay()
        {
            if (S.Phase != Phases.Plan) return false;
            S.Phase = Phases.Live;
            S.Paused = false;
            double avail = 0;
            foreach (var s in S.Staff) if (RoleOf(s).Rate > 0 && !s.Sick) avail += s.HoursLeft;
            S.Today.HoursAvail = avail;
            Emit("day_live", S.Day);
            return true;
        }

        void EndDay()
        {
            S.Minute = Gd.DayEnd;
            for (int i = S.Matters.Count - 1; i >= 0; i--)
                if (i < S.Matters.Count && S.Matters[i].Status == MatterStatus.Hearing) Resolve(S.Matters[i].Id, 0);
            var td = S.Today;
            bool forgiving = S.Tutorial || S.Day <= Gd.ForgivingDays;
            // soft deadlines: a missed filing / evidence / motion is sanctioned once and stays on the rail (never deleted)
            foreach (var m in S.Matters)
            {
                if (m.Status != MatterStatus.Open) continue;
                for (int i = 0; i < m.Tasks.Count; i++)
                {
                    var t = m.Tasks[i];
                    if (t.Finished || t.Late || t.Kind == Kinds.Hearing || t.Kind == Kinds.Prep || t.Due > S.Day) continue;
                    t.Late = true;
                    td.Late++;
                    if (!forgiving)
                    {
                        double fine = Math.Round(Gd.Sanction * TierOf(m).Fee);
                        S.Cash -= fine;
                        td.Sanctions += fine;
                        AddStanding(m.Area, Gd.StLate);
                    }
                    Emit("late", m.Id, i, 0, t.Kind);
                }
            }
            // clients still waiting come back tomorrow morning
            foreach (var m in S.Matters)
                if (m.Status == MatterStatus.Intake && !m.Tutorial)
                    m.Expires = Gd.DayStart + Gd.PatienceMin + Gd.PatiencePerReceptionist * Math.Min(Gd.MaxReceptionists, CountRole("receptionist"));
            foreach (var s in S.Staff)
                if (!IsFounder(s) && s.TempUntil <= 0) td.Salaries += RoleOf(s).Salary;
            td.Rent = Gd.Rent[Math.Max(0, Math.Min(Gd.Rent.Length - 1, S.Chapter - 1))];
            S.Cash -= td.Salaries + td.Rent;
            foreach (var s in S.Staff)
                s.Fatigue = Math.Max(0, s.Fatigue - Gd.FatRecover - Gd.FatRecoverFree * Math.Max(0, s.HoursLeft));
            // day passes end
            for (int i = S.Staff.Count - 1; i >= 0; i--)
            {
                var s = S.Staff[i];
                if (s.TempUntil > 0 && s.TempUntil <= S.Day) { UnassignAll(s.Id); S.Staff.RemoveAt(i); Emit("temp_left", s.Id); }
            }
            int misses = td.Late + td.NoLawyer;
            td.StarDeadlines = misses == 0 ? 3 : misses == 1 ? 2 : misses == 2 ? 1 : 0;
            int happy = td.Wins + td.Settles + td.Accepted + td.Referred, unhappy = td.WalkedOut + td.Losses;
            td.StarClients = happy + unhappy == 0 ? 2 : StarsAbove((double)happy / (happy + unhappy), Gd.StarClients);
            td.StarBilling = StarsAbove(td.Utilization, Gd.StarBilling);
            if (td.Stars > S.BestDayStars) S.BestDayStars = td.Stars;
            S.History.Add(td);
            while (S.History.Count > 14) S.History.RemoveAt(0);
            S.Billed = false;
            S.Phase = Phases.Report;
            Emit("day_done", S.Day, td.Stars, td.Net);
        }

        static int StarsAbove(double x, double[] th)
        {
            int s = 0;
            for (int i = 0; i < th.Length; i++) if (x >= th[i] - 1e-12) s = i + 1;
            return s;
        }

        void UnassignAll(int staffId)
        {
            foreach (var m in S.Matters) foreach (var t in m.Tasks) if (t.Staff == staffId) t.Staff = Attend.None;
        }

        /// <summary>The billing run (report screen): today's invoices + fees move to the operating account.</summary>
        public double Bill()
        {
            if (S.Phase != Phases.Report || S.Billed) return -1;
            double a = Math.Round(S.Wip, 2);
            S.Cash += a;
            S.Today.Invoiced = a;
            S.Wip = 0;
            S.Billed = true;
            Emit("billed", S.Day, -1, a);
            return a;
        }

        /// <summary>Report -> next morning's plan (an unbilled day is billed on the way: nothing is ever lost).</summary>
        public bool NextDay()
        {
            if (S.Phase != Phases.Report) return false;
            if (!S.Billed) Bill();
            S.Day++;
            S.Minute = Gd.DayStart;
            S.Today = new DayStats { Day = S.Day };
            S.Billed = false;
            S.Boosted = false;
            foreach (var s in S.Staff) { s.Sick = false; s.HoursLeft = RoleOf(s).Hours; s.HoursWorked = 0; }
            S.Events.RemoveAll(e => e.UntilDay < S.Day);
            foreach (var m in S.Matters)
            {
                var h = m.HearingTask;
                if (m.Status == MatterStatus.Open && h != null && h.Due < S.Day) h.Due = S.Day;   // guard: a court date never lies in the past
            }
            PlanDay();
            S.Phase = Phases.Plan;
            S.Paused = false;
            lastAutoMinute = -1;
            Emit("day_start", S.Day);
            if (!S.Tutorial)
            {
                int morning = Gd.MorningWalkins + (HasRole("receptionist") ? 1 : 0);
                for (int i = 0; i < morning; i++) Walkin();
            }
            return true;
        }

        // ------------------------------------------------------------------------------------------------ money

        /// <summary>Gross income of a usual day (avg of the last 3 reports, floor) — the unit of packs and rewards.</summary>
        public double DayIncome()
        {
            double sum = 0; int n = 0;
            for (int i = S.History.Count - 1; i >= 0 && n < 3; i--, n++) sum += S.History[i].Income;
            double avg = n > 0 ? sum / n : 0;
            return Math.Max(Gd.DayIncomeFloor, avg);
        }

        public double AvgNet(int days = 3)
        {
            double sum = 0; int n = 0;
            for (int i = S.History.Count - 1; i >= 0 && n < days; i--, n++) sum += S.History[i].Net;
            return n > 0 ? sum / n : 0;
        }

        /// <summary>Consumable cash pack: days × the usual day income, straight to the operating account (never to trust).</summary>
        public double GrantCash(double days)
        {
            double a = Math.Round(Math.Max(0, days) * DayIncome());
            S.Cash += a;
            Emit("cash_pack", -1, -1, a);
            return a;
        }

        /// <summary>Starter pack's intern day pass: an extra intern for today (or tomorrow, after the day ended).</summary>
        public StaffState AddDayPass()
        {
            var st = AddStaff("intern");
            st.TempUntil = S.Phase == Phases.Report ? S.Day + 1 : S.Day;
            st.HoursLeft = S.Phase == Phases.Live ? RoleOf(st).Hours * Math.Max(0, Gd.DayEnd - S.Minute) / Gd.DayMinutes : RoleOf(st).Hours;
            Emit("hired", st.Id, -1, 0, "day_pass");
            return st;
        }

        // ------------------------------------------------------------------------------------------------ hiring

        public BuyInfo CanHire(string role)
        {
            var r = new BuyInfo();
            var d = Gd.Role(role);
            if (d == null) { r.Reason = "invalid"; return r; }
            if (d.Chapter > S.Chapter) { r.Reason = "locked"; return r; }
            if (S.Staff.FindAll(x => x.TempUntil <= 0).Count >= DeskCap) { r.Reason = "desks"; return r; }
            if (role == "receptionist" && CountRole("receptionist") >= Gd.MaxReceptionists) { r.Reason = "max"; return r; }
            r.Cost = d.Hire;
            if (S.Cash < r.Cost) { r.Reason = "cash"; return r; }
            r.Ok = true;
            return r;
        }

        public string Hire(string role)
        {
            var info = CanHire(role);
            if (!info.Ok) return info.Reason;
            S.Cash -= info.Cost;
            var st = AddStaff(role);
            var d = RoleOf(st);
            st.HoursLeft = S.Phase == Phases.Live ? d.Hours * Math.Max(0, Gd.DayEnd - S.Minute) / Gd.DayMinutes : S.Phase == Phases.Plan ? d.Hours : 0;
            if (S.Phase == Phases.Live && d.Rate > 0) S.Today.HoursAvail += st.HoursLeft;
            Emit("hired", st.Id, -1, info.Cost, role);
            return "";
        }

        public string Fire(int staffId)
        {
            var s = StaffById(staffId);
            if (s == null) return "invalid";
            if (IsFounder(s)) return "founder";
            if (InSession(staffId)) return "in_session";
            UnassignAll(staffId);
            S.Staff.Remove(s);
            Emit("fired", staffId, -1, 0, s.Role);
            return "";
        }

        // ------------------------------------------------------------------------------------------------ rooms

        public BuyInfo CanBuild(string id)
        {
            var r = new BuyInfo();
            var d = Gd.Room(id);
            if (d == null) { r.Reason = "invalid"; return r; }
            if (RoomCount(id) >= d.Max) { r.Reason = "owned"; return r; }
            if (d.Chapter > S.Chapter) { r.Reason = "locked"; return r; }
            if (S.Building != "") { r.Reason = S.Building == id ? "building" : "busy"; return r; }
            r.Cost = d.Cost;
            if (S.Cash < r.Cost) { r.Reason = "cash"; return r; }
            r.Ok = true;
            return r;
        }

        public string Build(string id)
        {
            var info = CanBuild(id);
            if (!info.Ok) return info.Reason;
            S.Cash -= info.Cost;
            var d = Gd.Room(id);
            var rs = S.Rooms.Find(x => x.Id == id);
            if (rs == null) { rs = new RoomState { Id = id }; S.Rooms.Add(rs); }
            S.Building = id;
            rs.BuildLeftS = S.Tutorial ? 0 : d.BuildS;
            Emit("room_start", -1, -1, info.Cost, id);
            if (rs.BuildLeftS <= 0) FinishBuild();
            return "";
        }

        /// <summary>Real seconds pass for the build timer only (balance bot / tests that advance game minutes directly).</summary>
        public void AdvanceBuild(double realS) { if (realS > 0) TickBuild(realS); }

        void TickBuild(double realS)
        {
            if (S.Building == "") return;
            var rs = S.Rooms.Find(x => x.Id == S.Building);
            if (rs == null) { S.Building = ""; return; }
            rs.BuildLeftS -= realS;
            if (rs.BuildLeftS <= 0) FinishBuild();
        }

        void FinishBuild()
        {
            var rs = S.Rooms.Find(x => x.Id == S.Building);
            string id = S.Building;
            S.Building = "";
            if (rs == null) return;
            rs.BuildLeftS = 0;
            rs.Count++;
            Emit("room_done", -1, -1, rs.Count, id);
        }

        public double BuildLeftS { get { var rs = S.Building == "" ? null : S.Rooms.Find(x => x.Id == S.Building); return rs == null ? 0 : rs.BuildLeftS; } }

        // ------------------------------------------------------------------------------------------------ chapters

        public ChapterDef NextChapter => Gd.ChapterOf(S.Chapter + 1);

        /// <summary>"" = the firm may move up now; else max | standing | closed | cash.</summary>
        public string CanMoveUp()
        {
            var c = NextChapter;
            if (c == null) return "max";
            if (FirmStanding < c.Standing - 1e-9) return "standing";
            if (S.Closed < c.Closed) return "closed";
            if (S.Cash < c.Cost) return "cash";
            return "";
        }

        public string MoveUp()
        {
            string why = CanMoveUp();
            if (why != "") return why;
            var c = NextChapter;
            S.Cash -= c.Cost;
            S.Chapter = c.Chapter;
            FillUpcoming();
            Emit("chapter", c.Chapter, -1, c.Cost, c.Id);
            return "";
        }

        // ------------------------------------------------------------------------------------------------ standing instructions

        public int MaxRules => Gd.RulesFree + (Perk("extra_rule") ? Gd.RulesPremium : 0) + (Perk("partner_pass") ? Gd.RulesPass : 0) + (Perk("automation") ? Gd.RulesAutomation : 0);

        public string SetRule(string area, bool on)
        {
            if (!AreaUnlocked(area)) return "locked";
            if (!on) { S.Rules.Remove(area); return ""; }
            if (S.Rules.Contains(area)) return "";
            if (S.Rules.Count >= MaxRules) return "max";
            S.Rules.Add(area);
            return "";
        }

        // ------------------------------------------------------------------------------------------------ offline

        public void Touch(long now) { S.LastSeen = now; }

        /// <summary>Back from away: room builds catch up; the firm keeps earning by its standing instructions (rate 50 %,
        /// cap 4 h; Overnight+ 8 h). The calendar itself never moves while you are away, so no deadline can be missed
        /// offline. Null when nothing was earned.</summary>
        public OfflineSummary Offline(long now)
        {
            if (S.LastSeen <= 0) { S.LastSeen = now; return null; }
            double away = now - S.LastSeen;
            S.LastSeen = now;
            if (away < Gd.OfflineMinS) return null;
            TickBuild(away);
            double capH = Perk("overnight_plus") ? Gd.OfflineCapHPlus : Gd.OfflineCapH;
            double hours = Math.Min(away / 3600.0, capH);
            double share = S.Rules.Count == 0 ? 0.25 : Math.Min(1.0, 0.5 + 0.5 * S.Rules.Count / Math.Max(1, MaxRules));
            double amount = Math.Round(Math.Max(0, AvgNet()) * hours * Gd.OfflineDaysPerHour * Gd.OfflineRate * share);
            if (amount <= 0) return null;
            S.Cash += amount;
            S.OfflinePending = amount;
            return new OfflineSummary { Seconds = away, Hours = hours, Amount = amount, Capped = away / 3600.0 > capH };
        }

        // ------------------------------------------------------------------------------------------------ rewards (sim side)

        public static readonly string[] RewardPlacements = { "billing_boost", "case_preview", "offline_x2" };
        public static bool RewardIsCash(string placement) => placement == "billing_boost" || placement == "offline_x2";

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
                case "billing_boost":
                    if (S.Phase != Phases.Report || S.Billed || S.Boosted || S.Wip <= 0) { o.Reason = "context"; return o; }
                    o.Amount = Math.Round(Math.Min(def.Share * S.Wip, single));
                    if (o.Amount <= 0) { o.Reason = "context"; return o; }
                    break;
                case "case_preview":
                {
                    bool any = false;
                    foreach (var m in Upcoming) if (!m.Revealed) any = true;
                    if (!any) { o.Reason = "context"; return o; }
                    break;
                }
                case "offline_x2":
                    if (S.OfflinePending <= 0) { o.Reason = "context"; return o; }
                    o.Amount = Math.Round(Math.Min(S.OfflinePending, single));
                    break;
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
                case "billing_boost":
                    S.Wip += o.Amount; S.Today.Boost += o.Amount; S.Boosted = true; r.Amount = o.Amount;
                    break;
                case "case_preview":
                {
                    int n = 0;
                    foreach (var m in Upcoming) if (!m.Revealed) { m.Revealed = true; n++; }
                    r.Amount = n;
                    break;
                }
                case "offline_x2":
                    S.Cash += o.Amount; S.OfflinePending = 0; r.Amount = o.Amount;
                    break;
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
    }
}
