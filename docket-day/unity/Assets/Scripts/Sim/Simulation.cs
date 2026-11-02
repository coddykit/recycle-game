// Docket Day sim core — engine independent (no UnityEngine). C# 9 / .NET Standard 2.1.
// One career: a small law firm in the fictional harbour city of Calderport. A work day runs 09:00-18:00 game time
// (3 real minutes at 1x). The job is not waiting for timers: it is fitting people-hours into a calendar of dated
// milestones (filing windows, evidence, prep, court dates) that collides with itself.
//   plan  (09:00, paused)  — intake decisions, who works on what (the Docket Rail), settle or litigate;
//   live                   — staff burn their daily hours on their tasks (earliest deadline first), clients walk in,
//                            hearings start at 10:00 / 14:00 and end in a verdict;
//   report (18:00)         — late filings are sanctioned, salaries + rent paid, stars; the billing run turns the day's
//                            work into invoices; NextDay().
// Partial class: this file = construction, queries, time, work; Matters.cs = intake, matters, hearings, odds,
// collisions, assignment; Economy.cs = day end, billing, hiring, rooms, chapters, offline, rewards; Crises.cs = events.
using System;
using System.Collections.Generic;

namespace DocketDay.Sim
{
    public sealed class SimEvent
    {
        public string Type;     // walkin, walked_out, accepted, declined, referred, task_done, revealed, hearing_start, verdict, settled,
                                // continued, late, crisis, room_done, chapter, hired, fired, billed, day_live, day_done, day_start, reward, ...
        public int A = -1, B = -1;
        public double V;
        public string Text = "";
        public override string ToString() => Type + "(" + A + "," + B + "," + V + "," + Text + ")";
    }

    public sealed partial class Simulation
    {
        public const int NamePool = 24;
        public readonly GameData Gd;
        public State S;
        public readonly Rng Rng;
        readonly List<SimEvent> events = new List<SimEvent>();
        readonly Dictionary<string, bool> perks = new Dictionary<string, bool>();
        int lastAutoMinute = -1;

        public Simulation(GameData gd, long seed)
        {
            Gd = gd ?? throw new ArgumentNullException(nameof(gd));
            S = new State { Seed = seed <= 0 ? 1 : seed };
            Rng = new Rng(S.Seed);
            NewCareer();
            S.RngState = Rng.State;
        }

        /// <summary>From a decoded save (SaveCodec).</summary>
        public Simulation(GameData gd, State s)
        {
            Gd = gd ?? throw new ArgumentNullException(nameof(gd));
            S = s;
            Rng = new Rng(1) { State = s.RngState <= 0 ? 1 : s.RngState };
            if (S.Standing == null || S.Standing.Length != Gd.Areas.Count)
            {
                var st = new double[Gd.Areas.Count];
                for (int i = 0; i < st.Length; i++) st[i] = S.Standing != null && i < S.Standing.Length ? S.Standing[i] : Gd.StartStanding;
                S.Standing = st;
            }
        }

        public JObj Snapshot()
        {
            S.RngState = Rng.State;
            return S.ToJson();
        }

        void NewCareer()
        {
            S.Day = 1;
            S.Minute = Gd.DayStart;
            S.Phase = Phases.Plan;
            S.Cash = Gd.StartCash;
            S.Chapter = 1;
            S.Standing = new double[Gd.Areas.Count];
            for (int i = 0; i < S.Standing.Length; i++) S.Standing[i] = Gd.StartStanding;
            var usedNames = new List<int>();
            foreach (var role in Gd.StartStaff)
            {
                var st = AddStaff(role, usedNames);
                st.HoursLeft = RoleOf(st).Hours;
            }
            S.Today = new DayStats { Day = 1 };
            S.Tutorial = true;
            FillUpcoming();
            SeedTutorial();
        }

        StaffState AddStaff(string role, List<int> usedNames = null)
        {
            int name = 0;
            if (S.NextStaff > 0)
            {
                // a free name from the pool (0 is the founder's "You")
                for (int k = 0; k < 40; k++)
                {
                    name = 1 + Rng.RangeI(0, NamePool - 2);
                    if (!S.Staff.Exists(x => x.Name == name) && (usedNames == null || !usedNames.Contains(name))) break;
                }
            }
            usedNames?.Add(name);
            var st = new StaffState { Id = S.NextStaff++, Role = role, Name = name };
            S.Staff.Add(st);
            return st;
        }

        // ------------------------------------------------------------------------------------------------ queries

        public List<SimEvent> DrainEvents()
        {
            var o = new List<SimEvent>(events);
            events.Clear();
            return o;
        }

        void Emit(string type, int a = -1, int b = -1, double v = 0, string text = "") => events.Add(new SimEvent { Type = type, A = a, B = b, V = v, Text = text ?? "" });

        public string Phase => S.Phase;
        public int Chapter => S.Chapter;
        public bool Perk(string id) => perks.TryGetValue(id, out bool b) && b;
        public int MaxSpeed => Perk("speed") ? Gd.PaidMaxSpeed : Gd.FreeMaxSpeed;
        public int Weekday => (S.Day - 1) % Gd.WeekDays;
        public int WeekdayOf(int day) => (Math.Max(1, day) - 1) % Gd.WeekDays;
        public int Week => (S.Day - 1) / Gd.WeekDays + 1;
        public int RailDays => Perk("partner_pass") ? Gd.RailDaysPass : Gd.RailDays;
        public RoleDef RoleOf(StaffState s) => Gd.Role(s.Role) ?? Gd.Roles["intern"];
        public StaffState StaffById(int id) => S.Staff.Find(x => x.Id == id);
        public MatterState MatterById(int id) => S.Matters.Find(m => m.Id == id);
        public bool IsFounder(StaffState s) => s.Id == 0;
        public AreaDef AreaOf(MatterState m) => Gd.Area(m.Area) ?? Gd.Areas[0];
        public TierDef TierOf(MatterState m) => Gd.Tier(m.Tier) ?? Gd.Tiers["S"];
        public JudgeDef JudgeOf(MatterState m) => Gd.Judges[Math.Max(0, Math.Min(Gd.Judges.Count - 1, m.Judge))];
        public bool AreaUnlocked(string area) { var a = Gd.Area(area); return a != null && a.Chapter <= S.Chapter; }
        public double AreaStanding(string area) { int i = Gd.AreaIndex(area); return i >= 0 && i < S.Standing.Length ? S.Standing[i] : 0; }
        public double FeeMult(string area) => 1.0 + Gd.StandingFee * AreaStanding(area);

        /// <summary>Firm Standing = the sum over the practice areas the firm has opened (chapter gates).</summary>
        public double FirmStanding
        {
            get
            {
                double s = 0;
                for (int i = 0; i < Gd.Areas.Count; i++) if (Gd.Areas[i].Chapter <= S.Chapter) s += S.Standing[i];
                return s;
            }
        }

        public double AvgStanding
        {
            get
            {
                double s = 0; int n = 0;
                for (int i = 0; i < Gd.Areas.Count; i++) if (Gd.Areas[i].Chapter <= S.Chapter) { s += S.Standing[i]; n++; }
                return n > 0 ? s / n : 0;
            }
        }

        public IEnumerable<MatterState> Intakes { get { foreach (var m in S.Matters) if (m.Status == MatterStatus.Intake) yield return m; } }
        public IEnumerable<MatterState> Upcoming { get { foreach (var m in S.Matters) if (m.Status == MatterStatus.Upcoming) yield return m; } }
        public IEnumerable<MatterState> Active { get { foreach (var m in S.Matters) if (m.Active) yield return m; } }
        public int IntakeCount { get { int n = 0; foreach (var _ in Intakes) n++; return n; } }
        public int ActiveCount { get { int n = 0; foreach (var _ in Active) n++; return n; } }
        public double TrustTotal { get { double t = 0; foreach (var m in S.Matters) t += m.Trust; return t; } }
        public int CountRole(string role) => S.Staff.FindAll(x => x.Role == role).Count;
        public bool HasRole(string role) => S.Staff.Exists(x => x.Role == role);

        public int RoomCount(string id) { var r = S.Rooms.Find(x => x.Id == id); return r == null ? 0 : r.Count; }
        public bool RoomBuilt(string id) => RoomCount(id) > 0;
        double RoomMult(Func<RoomDef, double> f)
        {
            double m = 1;
            foreach (var r in S.Rooms) { var d = Gd.Room(r.Id); if (d != null && r.Count > 0) m *= f(d); }
            return m;
        }
        public double WorkMult(string kind)
        {
            switch (Kinds.Skill(kind))
            {
                case Kinds.File: return RoomMult(d => d.File);
                case Kinds.Evidence: return RoomMult(d => d.Evidence);
                case Kinds.Prep: return RoomMult(d => d.Prep);
            }
            return 1;
        }
        public double FatigueGainMult => RoomMult(d => d.Fatigue);
        public double SettleRoomMult => RoomMult(d => d.Settle);
        public double RoomOdds { get { double o = 0; foreach (var r in S.Rooms) { var d = Gd.Room(r.Id); if (d != null && r.Count > 0) o += d.Odds; } return o; } }

        public int DeskCap
        {
            get
            {
                var c = Gd.ChapterOf(S.Chapter);
                int desks = c != null ? c.Desks : 5;
                foreach (var r in S.Rooms) { var d = Gd.Room(r.Id); if (d != null) desks += d.Desks * r.Count; }
                return desks;
            }
        }

        public double FatigueMult(StaffState s) => s.Fatigue >= Gd.FatSpent ? Gd.FatSpentMult : s.Fatigue >= Gd.FatTired ? Gd.FatTiredMult : 1.0;
        public bool Tired(StaffState s) => s.Fatigue >= Gd.FatTired;

        /// <summary>Is this staff member in a hearing right now (the court takes their time)?</summary>
        public bool InSession(int staffId)
        {
            foreach (var m in S.Matters)
                if (m.Status == MatterStatus.Hearing) { var h = m.HearingTask; if (h != null && h.Staff == staffId) return true; }
            return false;
        }

        // ------------------------------------------------------------------------------------------------ time

        public void SetSpeed(int speed) => S.Speed = Math.Max(1, Math.Min(MaxSpeed, speed));
        public void SetPaused(bool paused) => S.Paused = paused;

        /// <summary>Store perks (never saved): speed, automation, overnight_plus, extra_rule, partner_pass.</summary>
        public void SetPerks(Dictionary<string, bool> p)
        {
            perks.Clear();
            if (p != null) foreach (var kv in p) perks[kv.Key] = kv.Value;
            if (S.Speed > MaxSpeed) S.Speed = MaxSpeed;
            while (S.Rules.Count > MaxRules) S.Rules.RemoveAt(S.Rules.Count - 1);
        }

        /// <summary>Every frame with real seconds. Room builds run on real time in every phase; the day only while live.</summary>
        public void Advance(double realDt)
        {
            if (realDt <= 0 || double.IsNaN(realDt)) return;
            realDt = Math.Min(realDt, 1.0);   // a hitch never jumps the sim
            TickBuild(realDt);
            if (S.Phase != Phases.Live || S.Paused) return;
            double gm = realDt * Gd.GameMinPerRealS * Math.Max(1, S.Speed);
            while (gm > 1e-9 && S.Phase == Phases.Live)
            {
                double step = Math.Min(1.0, gm);
                Step(step);
                gm -= step;
            }
        }

        /// <summary>Advances game minutes directly (tests, guide fast-forward, fast-forward to the next decision). Stops at the day end.</summary>
        public void AdvanceMinutes(double minutes)
        {
            while (minutes > 1e-9 && S.Phase == Phases.Live)
            {
                double step = Math.Min(1.0, minutes);
                Step(step);
                minutes -= step;
            }
        }

        void Step(double dt)
        {
            RunPlanned();
            ExpireIntakes();
            StartHearings();
            EndSessions();
            Work(dt);
            int minute = (int)S.Minute;
            if (minute / 10 != lastAutoMinute / 10) { lastAutoMinute = minute; AutoStaff(); }
            S.Minute += dt;
            if (S.Minute >= Gd.DayEnd - 1e-9) EndDay();
        }

        // ------------------------------------------------------------------------------------------------ work

        bool PowerCut()
        {
            foreach (var e in S.Events) if (e.Id == "power_cut" && e.UntilDay >= S.Day && S.Minute >= e.From && S.Minute < e.To) return true;
            return false;
        }

        /// <summary>The task a person works on next: their assigned open work task with the earliest deadline.</summary>
        public (MatterState m, int task) NextTaskOf(int staffId)
        {
            MatterState best = null; int bt = -1; int bdue = int.MaxValue;
            foreach (var m in S.Matters)
            {
                if (m.Status != MatterStatus.Open) continue;
                for (int i = 0; i < m.Tasks.Count; i++)
                {
                    var t = m.Tasks[i];
                    if (t.Staff != staffId || t.Finished || !Kinds.Work(t.Kind)) continue;
                    if (t.Due < bdue || (t.Due == bdue && best != null && m.Id < best.Id)) { best = m; bt = i; bdue = t.Due; }
                }
            }
            return (best, bt);
        }

        /// <summary>Hours of assigned, unfinished work queued on a person (at their skill).</summary>
        public double QueuedHours(StaffState s)
        {
            double h = 0;
            var role = RoleOf(s);
            foreach (var m in S.Matters)
            {
                if (m.Status != MatterStatus.Open) continue;
                foreach (var t in m.Tasks)
                {
                    if (t.Staff != s.Id || t.Finished || !Kinds.Work(t.Kind)) continue;
                    double sk = role.Skill(t.Kind) * WorkMult(t.Kind);
                    h += sk > 0 ? t.Left / sk : 0;
                }
            }
            return h;
        }

        void Work(double dt)
        {
            bool cut = PowerCut();
            foreach (var st in S.Staff)
            {
                if (st.Sick || st.HoursLeft <= 1e-9 || InSession(st.Id)) continue;
                var (m, ti) = NextTaskOf(st.Id);
                if (m == null) continue;
                var t = m.Tasks[ti];
                var role = RoleOf(st);
                double h = Math.Min(dt / 60.0, st.HoursLeft);
                double eff = role.Skill(t.Kind) * WorkMult(t.Kind) * FatigueMult(st);
                if (cut && Kinds.Skill(t.Kind) == Kinds.File) eff = 0;   // no printers, no filings this hour
                st.HoursLeft -= h;
                st.HoursWorked += h;
                S.Today.HoursWorked += h;
                st.Fatigue = Math.Min(100, st.Fatigue + h * Gd.FatPerHour * FatigueGainMult);
                BillHours(m, h * role.Rate);
                t.Done += h * eff;
                if (t.Done >= t.Need - 1e-9)
                {
                    t.Done = t.Need;
                    t.Finished = true;
                    Emit("task_done", m.Id, ti, 0, t.Kind);
                    if (t.Kind == Kinds.Evidence && !m.Revealed) { m.Revealed = true; Emit("revealed", m.Id, -1, m.Twist); }
                }
            }
        }

        /// <summary>Hourly work goes on today's invoices; retainer work is drawn from the client's trust money first (only
        /// fees ever move to the operating account); contingency work is not billed (the fee comes with the result).</summary>
        void BillHours(MatterState m, double amount)
        {
            if (amount <= 0 || m.Fee == Fees.Contingency) return;
            amount *= FeeMult(m.Area);
            if (m.Fee == Fees.Retainer)
            {
                double draw = Math.Min(m.Trust, amount);
                m.Trust -= draw;
            }
            S.Wip += amount;
            m.Billed += amount;
            S.Today.Billed += amount;
        }
    }
}
