// Docket Day sim core — headless balance bot (P1: 30 min / 2 h / 7 days). Plays like a sensible new player: takes the
// clients it has hours for, refers conflicts, staffs the rail (the same auto-assign the Office Manager uses), covers
// collisions (free lawyer -> visiting counsel -> continuance), settles when the settlement beats the expected verdict,
// does the prep table for most matters, runs the billing run, builds rooms, hires when the team is busy and moves up a
// chapter when it can. Deterministic per seed.
using System;
using System.Collections.Generic;
using System.Text;

namespace DocketDay.Sim
{
    public sealed class BalanceReport
    {
        public readonly List<DayStats> Days = new List<DayStats>();
        public readonly List<double> StandingByDay = new List<double>();
        public double Cash, Standing;
        public int Chapter, Staff, Rooms, Closed, Wins, Losses, Settles;
        public double MinCash = double.MaxValue;
        public bool Finite = true;
        public readonly List<string> Log = new List<string>();

        public string Table()
        {
            var sb = new StringBuilder();
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            sb.AppendLine("day walk acc  W/L/S late util%  billed  fees  costs   net  stars standing");
            for (int i = 0; i < Days.Count; i++)
            {
                var d = Days[i];
                sb.AppendLine(string.Format(ci, "{0,3} {1,4} {2,3} {3,2}/{4}/{5} {6,4} {7,5:0} {8,7:0} {9,5:0} {10,6:0} {11,5:0}   {12}/9  {13,5:0.00}",
                    d.Day, d.Walkins, d.Accepted, d.Wins, d.Losses, d.Settles, d.Late, d.Utilization * 100, d.Billed, d.Fees + d.Boost, d.Costs, d.Net, d.Stars,
                    i < StandingByDay.Count ? StandingByDay[i] : 0));
            }
            sb.AppendLine(string.Format(ci, "cash {0:0} standing {1:0.00} chapter {2} staff {3} rooms {4} closed {5} (W {6} L {7} S {8}) minCash {9:0}",
                Cash, Standing, Chapter, Staff, Rooms, Closed, Wins, Losses, Settles, MinCash));
            return sb.ToString();
        }
    }

    public static class BalanceRunner
    {
        static readonly string[] RoomOrder = { "library", "coffee", "docroom", "desk", "evidence", "conference", "desk", "war_room", "desk" };

        public static BalanceReport Run(GameData gd, long seed, int days, bool skipTutorial = true, Action<Simulation> eachDay = null)
        {
            var sim = new Simulation(gd, seed);
            if (skipTutorial)
            {
                sim.EndTutorial();
                var t = sim.MatterById(sim.S.TutorialMatter);
                if (t != null) t.Tutorial = false;   // the scripted ticket becomes an ordinary first client
            }
            var rep = new BalanceReport();
            while (sim.S.Day <= days)
            {
                eachDay?.Invoke(sim);
                PlayDay(sim, rep);
                rep.Days.Add(sim.S.History[sim.S.History.Count - 1]);
                rep.StandingByDay.Add(sim.FirmStanding);
                if (sim.S.Day >= days) break;
                sim.NextDay();
            }
            rep.Cash = sim.S.Cash; rep.Standing = sim.FirmStanding; rep.Chapter = sim.Chapter; rep.Staff = sim.S.Staff.Count;
            foreach (var r in sim.S.Rooms) rep.Rooms += r.Count;
            rep.Closed = sim.S.Closed; rep.Wins = sim.S.Wins; rep.Losses = sim.S.Losses; rep.Settles = sim.S.Settles;
            return rep;
        }

        public static void PlayDay(Simulation sim, BalanceReport rep)
        {
            Manage(sim, rep, true);
            sim.StartDay();
            int guard = 0;
            while (sim.Phase == Phases.Live && guard++ < 100000)
            {
                sim.AdvanceMinutes(5);
                sim.AdvanceBuild(5 / sim.Gd.GameMinPerRealS);   // builds run on real time: 5 game min = 1.7 s at 1×
                sim.DrainEvents();
                Manage(sim, rep, false);
                if (double.IsNaN(sim.S.Cash) || double.IsInfinity(sim.S.Cash)) rep.Finite = false;
                rep.MinCash = Math.Min(rep.MinCash, sim.S.Cash);
            }
            sim.Bill();
            sim.DrainEvents();
            Manage(sim, rep, true);
        }

        /// <summary>Hours of unfinished work on open matters vs the team's daily hours.</summary>
        public static double BacklogDays(Simulation sim)
        {
            double need = 0, daily = 0;
            foreach (var m in sim.Active)
                foreach (var t in m.Tasks)
                    if (Kinds.Work(t.Kind) && !t.Finished) need += t.Left;
            foreach (var s in sim.S.Staff) if (sim.RoleOf(s).Rate > 0) daily += sim.RoleOf(s).Hours;
            return daily <= 0 ? 99 : need / daily;
        }

        static void Manage(Simulation sim, BalanceReport rep, bool calm)
        {
            var s = sim.S;
            if (s.Phase == Phases.Report) { sim.Bill(); }
            // intake
            foreach (var m in new List<MatterState>(sim.Intakes))
            {
                if (sim.Conflict(m)) { sim.Refer(m.Id); continue; }
                if (BacklogDays(sim) < 1.6) sim.Accept(m.Id);
            }
            // the prep table (most of the time)
            foreach (var m in new List<MatterState>(sim.Active))
                if (sim.PrepOpen(m) && m.Id % 5 < 3)
                    for (int c = 0; c < m.Claims; c++) sim.PrepMatch(m.Id, c, c);
            sim.AutoAssign(true);
            // collisions: a free lawyer, else visiting counsel, else a continuance
            foreach (var c in sim.Collisions())
            {
                var m = sim.MatterById(c.Matter);
                if (m == null || m.Status != MatterStatus.Open) continue;
                if (!sim.InCollision(m.Id)) continue;
                bool fixedIt = false;
                foreach (var st in s.Staff)
                {
                    if (!sim.RoleOf(st).Lawyer || st.Id == m.HearingTask.Staff || (st.Sick && c.Day == s.Day)) continue;
                    int busy = 0;
                    foreach (var o in sim.Active) { var h = o.HearingTask; if (o != m && h.Staff == st.Id && h.Due == c.Day && h.Slot == c.Slot) busy++; }
                    if (busy > 0) continue;
                    sim.Assign(st.Id, m.Id, m.Tasks.Count - 1);
                    fixedIt = true;
                    break;
                }
                if (fixedIt) continue;
                if (c.Day <= s.Day + 1 && s.Cash > sim.VisitingCost(m.Id) + 400) { sim.Visiting(m.Id); continue; }
                if (c.Day <= s.Day + 1 && sim.Continuance(m.Id) == "") continue;
            }
            // settle when the settlement beats the expected verdict (hearing tomorrow or today)
            foreach (var m in new List<MatterState>(sim.Active))
            {
                if (m.Status != MatterStatus.Open || m.HearingTask.Due > s.Day + 1) continue;
                double p = sim.ShownOdds(m);
                if (sim.SettleFee(m) > p * sim.WinFee(m) + (1 - p) * sim.LossFee(m)) sim.Settle(m.Id);
            }
            if (!calm) return;
            if (s.Rules.Count == 0) sim.SetRule("traffic", true);
            double buffer = Math.Max(600, sim.DayIncome() * 0.8);
            if (sim.CanMoveUp() == "" && s.Cash - sim.NextChapter.Cost > buffer) { sim.MoveUp(); rep.Log.Add("day " + s.Day + " chapter " + s.Chapter); }
            foreach (var r in RoomOrder)
            {
                var b = sim.CanBuild(r);
                if (b.Reason == "owned") continue;
                if (b.Ok && s.Cash - b.Cost > buffer) { sim.Build(r); rep.Log.Add("day " + s.Day + " room " + r); }
                break;
            }
            var last = s.History.Count > 0 ? s.History[s.History.Count - 1] : null;
            if (last != null && (last.Utilization >= 0.72 || BacklogDays(sim) > 1.2))
            {
                // the busiest kind of work decides who to hire
                double file = 0, ev = 0, prep = 0;
                foreach (var m in sim.Active)
                    foreach (var t in m.Tasks)
                    {
                        if (t.Finished) continue;
                        if (Kinds.Skill(t.Kind) == Kinds.File) file += t.Left;
                        else if (t.Kind == Kinds.Evidence) ev += t.Left;
                        else if (t.Kind == Kinds.Prep) prep += t.Left;
                    }
                string role = prep >= file && prep >= ev ? (sim.Chapter >= 3 && sim.CountRole("partner") < 2 ? "partner" : "associate")
                    : file >= ev ? (sim.Chapter >= 2 && !sim.HasRole("clerk") ? "clerk" : "paralegal")
                    : (sim.Chapter >= 2 && sim.CountRole("investigator") < 2 ? "investigator" : "intern");
                var h = sim.CanHire(role);
                if (h.Ok && s.Cash - h.Cost > buffer * 1.2) { sim.Hire(role); rep.Log.Add("day " + s.Day + " hire " + role); }
            }
            if (!sim.HasRole("receptionist") && s.Day >= 6 && sim.CanHire("receptionist").Ok && s.Cash > buffer * 2) sim.Hire("receptionist");
        }
    }
}
