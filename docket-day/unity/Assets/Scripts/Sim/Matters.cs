// Docket Day sim core — matters: generation (templates × tiers × judges × parties, a hidden twist), intake (walk-ins,
// conflict check, accept / decline / refer / ethical wall, standing instructions), the Docket Rail (who works on what,
// collisions, visiting counsel, continuances), the prep table, settle vs litigate, hearings and the odds model.
// Failure is soft everywhere: a missed filing costs a sanction and Standing, a missing lawyer gets one automatic
// continuance, a lost hearing still pays a small "lesson" fee. Nothing is ever deleted as a punishment.
using System;
using System.Collections.Generic;

namespace DocketDay.Sim
{
    public sealed class Collision
    {
        public int Matter, Day, Slot;
        public string Kind;      // double (one lawyer, two hearings) | uncovered (nobody attends)
        public int Staff = Attend.None;
    }

    public sealed partial class Simulation
    {
        // ------------------------------------------------------------------------------------------------ generation

        int PickArea()
        {
            var w = new double[Gd.Areas.Count];
            for (int i = 0; i < w.Length; i++) w[i] = Gd.Areas[i].Chapter <= S.Chapter ? Gd.Areas[i].Weight : 0;
            int a = Rng.PickWeighted(w);
            return a < 0 ? 0 : a;
        }

        bool PartyBusy(int p)
        {
            foreach (var m in S.Matters) if (m.Status != MatterStatus.Upcoming && (m.Client == p || m.Opponent == p)) return true;
            return false;
        }

        int FreeParty(int avoid)
        {
            int p = 0;
            for (int k = 0; k < 30; k++)
            {
                p = Rng.RangeI(0, Gd.Parties.Count - 1);
                if (p != avoid && !PartyBusy(p)) return p;
            }
            return p == avoid ? (p + 1) % Gd.Parties.Count : p;
        }

        MatterState Generate()
        {
            var a = Gd.Areas[PickArea()];
            double st = AreaStanding(a.Id);
            string tier = "S";
            if (st >= Gd.Tiers["L"].MinStanding && Rng.Frand() < Gd.TierLChance) tier = "L";
            else if (st >= Gd.Tiers["M"].MinStanding && Rng.Frand() < Gd.TierMChance) tier = "M";
            var judges = new List<int>();
            for (int j = 0; j < Gd.Judges.Count; j++) if (Gd.Judges[j].Chapter <= S.Chapter) judges.Add(j);
            var m = new MatterState
            {
                Id = S.NextId++, Area = a.Id, Tier = tier, Fee = a.Fee, Status = MatterStatus.Upcoming, Title = Rng.RangeI(0, a.Titles - 1),
                Judge = judges.Count > 0 ? judges[Rng.RangeI(0, judges.Count - 1)] : 0,
                Twist = Gd.Twists[Rng.RangeI(0, Gd.Twists.Length - 1)], Claims = Math.Min(5, a.Claims + (tier == "L" ? 1 : 0)),
            };
            m.Client = FreeParty(-1);
            m.Opponent = FreeParty(m.Client);
            return m;
        }

        void FillUpcoming()
        {
            int n = 0;
            foreach (var _ in Upcoming) n++;
            while (n++ < Gd.Upcoming) S.Matters.Add(Generate());
        }

        // ------------------------------------------------------------------------------------------------ intake

        /// <summary>A client walks in: the next upcoming matter waits at reception (patience in game minutes).</summary>
        public MatterState Walkin()
        {
            if (IntakeCount >= Gd.QueueMax) return null;
            MatterState m = null;
            foreach (var u in Upcoming) { m = u; break; }
            if (m == null) { FillUpcoming(); foreach (var u in Upcoming) { m = u; break; } }
            if (m == null) return null;
            m.Status = MatterStatus.Intake;
            // a deliberate conflict now and then (from chapter 2): the other side is a current client
            if (S.Chapter >= Gd.ConflictChapter && !S.Tutorial && Rng.Frand() < Gd.ConflictChance)
            {
                var act = new List<MatterState>(Active);
                if (act.Count > 0) { var o = act[Rng.RangeI(0, act.Count - 1)]; if (o.Client != m.Client) m.Opponent = o.Client; }
            }
            double start = Math.Max(S.Minute, Gd.DayStart);
            m.Expires = start + Gd.PatienceMin + Gd.PatiencePerReceptionist * Math.Min(Gd.MaxReceptionists, CountRole("receptionist"));
            S.Today.Walkins++;
            FillUpcoming();
            Emit("walkin", m.Id);
            // standing instructions: auto-accept plain S matters of the chosen areas (never conflicts, never in the guide)
            if (!S.Tutorial && m.Tier == "S" && S.Rules.Contains(m.Area) && !Conflict(m)) Accept(m.Id);
            return m;
        }

        /// <summary>Does this matter put the firm on both sides (the new client is the other side of an open matter, or the
        /// other side is one of our clients)?</summary>
        public bool Conflict(MatterState m)
        {
            foreach (var o in S.Matters)
            {
                if (o == m || !o.Active) continue;
                if (o.Client == m.Opponent || o.Opponent == m.Client) return true;
            }
            return false;
        }

        void ExpireIntakes()
        {
            for (int i = S.Matters.Count - 1; i >= 0; i--)
            {
                var m = S.Matters[i];
                if (m.Status != MatterStatus.Intake || m.Tutorial || m.Expires > S.Minute) continue;
                S.Matters.RemoveAt(i);
                S.Today.WalkedOut++;
                AddStanding(m.Area, Gd.StWalkedOut);
                Emit("walked_out", m.Id, -1, 0, m.Area);
            }
        }

        MatterState IntakeById(int id) { var m = MatterById(id); return m != null && m.Status == MatterStatus.Intake ? m : null; }

        /// <summary>"" = can accept; else conflict | invalid | wall_locked | cash.</summary>
        public string CanAccept(int id, bool wall = false)
        {
            var m = IntakeById(id);
            if (m == null) return "invalid";
            if (wall)
            {
                if (S.Chapter < Gd.ConflictChapter) return "wall_locked";
                if (S.Cash < Gd.WallCost) return "cash";
                return "";
            }
            return Conflict(m) ? "conflict" : "";
        }

        public string Accept(int id, bool wall = false)
        {
            string why = CanAccept(id, wall);
            if (why != "") return why;
            var m = IntakeById(id);
            if (wall) { m.Wall = true; S.Cash -= Gd.WallCost; S.Today.Walls += Gd.WallCost; }
            var a = AreaOf(m);
            var tier = TierOf(m);
            m.Status = MatterStatus.Open;
            m.AcceptDay = S.Day;
            m.Tasks.Clear();
            if (m.Tutorial)
            {
                // the guide's ticket: evidence today, the filing tomorrow (amber), the hearing tomorrow at 14:00
                m.Tasks.Add(new TaskState { Kind = Kinds.Evidence, Need = 2, Due = S.Day });
                m.Tasks.Add(new TaskState { Kind = Kinds.File, Need = 2, Due = S.Day + 1 });
                m.Tasks.Add(new TaskState { Kind = Kinds.Hearing, Need = 2, Due = S.Day + 1, Slot = Gd.HearingSlots.Length - 1, Staff = FounderOrLawyer() });
            }
            else
            {
                int after = Rng.RangeI(a.AfterMin, a.AfterMax);
                int hearing = S.Day + after;
                if (a.File > 0) m.Tasks.Add(new TaskState { Kind = Kinds.File, Need = Math.Round(a.File * tier.Hours, 2), Due = S.Day + (after + 1) / 2 });
                if (a.Evidence > 0) m.Tasks.Add(new TaskState { Kind = Kinds.Evidence, Need = Math.Round(a.Evidence * tier.Hours, 2), Due = hearing - 1 });
                if (a.Prep > 0) m.Tasks.Add(new TaskState { Kind = Kinds.Prep, Need = Math.Round(a.Prep * tier.Hours, 2), Due = hearing });
                int slot = Rng.RangeI(0, Gd.HearingSlots.Length - 1);
                m.Tasks.Add(new TaskState { Kind = Kinds.Hearing, Need = Math.Round(a.HearingH * tier.Hours, 2), Due = hearing, Slot = slot, Staff = DefaultAttendee(hearing, slot) });
                // the prep table: exhibits for every claim + one decoy, shuffled
                var ex = new List<int>();
                for (int i = 0; i <= m.Claims; i++) ex.Add(i);
                for (int i = ex.Count - 1; i > 0; i--) { int j = Rng.RangeI(0, i); (ex[i], ex[j]) = (ex[j], ex[i]); }
                m.Exhibits = ex;
            }
            if (m.Fee == Fees.Retainer)
            {
                m.Trust = Math.Round(a.Retainer * tier.Fee);
                Emit("trust_in", m.Id, -1, m.Trust);
            }
            S.Today.Accepted++;
            Emit("accepted", m.Id, -1, 0, m.Area);
            if (Perk("automation")) AutoAssign(true);
            return "";
        }

        public string Decline(int id)
        {
            var m = IntakeById(id);
            if (m == null) return "invalid";
            if (m.Tutorial) return "tutorial";
            S.Matters.Remove(m);
            S.Today.Declined++;
            Emit("declined", m.Id, -1, 0, m.Area);
            return "";
        }

        /// <summary>Refer to another (fictional) firm: a small referral fee and a little Standing — the clean way out of a conflict.</summary>
        public string Refer(int id)
        {
            var m = IntakeById(id);
            if (m == null) return "invalid";
            if (m.Tutorial) return "tutorial";
            S.Matters.Remove(m);
            double fee = Math.Round(Gd.ReferralFee * TierOf(m).Fee);
            S.Wip += fee;
            S.Today.Fees += fee;
            S.CareerFees += fee;
            S.Today.Referred++;
            AddStanding(m.Area, Gd.StReferral);
            Emit("referred", m.Id, -1, fee, m.Area);
            return "";
        }

        // ------------------------------------------------------------------------------------------------ the rail

        int FounderOrLawyer()
        {
            var f = StaffById(0);
            if (f != null && RoleOf(f).Lawyer) return 0;
            var l = S.Staff.Find(x => RoleOf(x).Lawyer);
            return l != null ? l.Id : Attend.None;
        }

        int HearingsAt(int staffId, int day, int slot, MatterState except = null)
        {
            int n = 0;
            foreach (var m in S.Matters)
            {
                if (!m.Active || m == except) continue;
                var h = m.HearingTask;
                if (h != null && h.Staff == staffId && h.Due == day && h.Slot == slot) n++;
            }
            return n;
        }

        /// <summary>The lawyer free at that court date with the most trial skill (the founder first); everyone booked ->
        /// the founder anyway (a collision the player sees on the rail).</summary>
        int DefaultAttendee(int day, int slot)
        {
            int best = Attend.None; double bs = -1;
            foreach (var s in S.Staff)
            {
                var r = RoleOf(s);
                if (!r.Lawyer || s.TempUntil > 0) continue;
                if (HearingsAt(s.Id, day, slot) > 0) continue;
                double score = r.Trial + (IsFounder(s) ? 0.001 : 0);
                if (score > bs) { bs = score; best = s.Id; }
            }
            return best != Attend.None ? best : FounderOrLawyer();
        }

        /// <summary>"" = this person may take this task. Hearings need a lawyer; work needs the skill.</summary>
        public string CanAssign(int staffId, int matterId, int task)
        {
            var m = MatterById(matterId);
            if (m == null || m.Status != MatterStatus.Open || task < 0 || task >= m.Tasks.Count) return "invalid";
            var t = m.Tasks[task];
            if (t.Finished) return "done";
            var s = StaffById(staffId);
            if (s == null) return "invalid";
            var r = RoleOf(s);
            if (t.Kind == Kinds.Hearing) return r.Lawyer ? "" : "not_lawyer";
            return r.Skill(t.Kind) > 0 ? "" : "no_skill";
        }

        public string Assign(int staffId, int matterId, int task)
        {
            string why = CanAssign(staffId, matterId, task);
            if (why != "") return why;
            var m = MatterById(matterId);
            m.Tasks[task].Staff = staffId;
            Emit("assigned", m.Id, task, staffId);
            return "";
        }

        public string Unassign(int matterId, int task)
        {
            var m = MatterById(matterId);
            if (m == null || m.Status != MatterStatus.Open || task < 0 || task >= m.Tasks.Count) return "invalid";
            m.Tasks[task].Staff = Attend.None;
            return "";
        }

        /// <summary>Collisions on the rail: one lawyer booked for two hearings at the same court slot, or a hearing nobody
        /// attends (also: the attendee is out sick today).</summary>
        public List<Collision> Collisions()
        {
            var o = new List<Collision>();
            var groups = new Dictionary<long, List<MatterState>>();
            int last = S.Day + RailDays - 1;
            foreach (var m in S.Matters)
            {
                if (m.Status != MatterStatus.Open) continue;
                var h = m.HearingTask;
                if (h == null || h.Due < S.Day || h.Due > last) continue;
                if (h.Due == S.Day && S.Phase != Phases.Plan && S.Minute >= Gd.HearingSlots[Math.Min(h.Slot, Gd.HearingSlots.Length - 1)]) continue;
                var s = h.Staff >= 0 ? StaffById(h.Staff) : null;
                if (h.Staff == Attend.Visiting) continue;
                if (s == null || !RoleOf(s).Lawyer || (s.Sick && h.Due == S.Day))
                {
                    o.Add(new Collision { Matter = m.Id, Day = h.Due, Slot = h.Slot, Kind = "uncovered" });
                    continue;
                }
                long key = ((long)h.Due * 16 + h.Slot) * 4096 + h.Staff;
                if (!groups.TryGetValue(key, out var l)) groups[key] = l = new List<MatterState>();
                l.Add(m);
            }
            foreach (var kv in groups)
                if (kv.Value.Count > 1)
                    foreach (var m in kv.Value) { var h = m.HearingTask; o.Add(new Collision { Matter = m.Id, Day = h.Due, Slot = h.Slot, Kind = "double", Staff = h.Staff }); }
            return o;
        }

        public bool InCollision(int matterId) => Collisions().Exists(c => c.Matter == matterId);

        /// <summary>Hire visiting counsel for one hearing: costs cash now, weaker in court than your own lawyers.</summary>
        public string Visiting(int matterId)
        {
            var m = MatterById(matterId);
            if (m == null || m.Status != MatterStatus.Open) return "invalid";
            var h = m.HearingTask;
            if (h.Staff == Attend.Visiting) return "owned";
            double fee = Math.Round(Gd.VisitingFee * TierOf(m).Hours);
            if (S.Cash < fee) return "cash";
            S.Cash -= fee;
            S.Today.Visiting += fee;
            h.Staff = Attend.Visiting;
            Emit("visiting", m.Id, -1, fee);
            return "";
        }

        public double VisitingCost(int matterId) { var m = MatterById(matterId); return m == null ? 0 : Math.Round(Gd.VisitingFee * TierOf(m).Hours); }

        /// <summary>Ask the court to move the hearing (once per matter): +3 days, a little Standing.</summary>
        public string Continuance(int matterId)
        {
            var m = MatterById(matterId);
            if (m == null || m.Status != MatterStatus.Open) return "invalid";
            if (m.Continued) return "used";
            if (m.Tutorial) return "tutorial";
            var h = m.HearingTask;
            h.Due += Gd.ContinuanceDays;
            m.Continued = true;
            AddStanding(m.Area, Gd.StContinuance);
            S.Today.Continuances++;
            Emit("continued", m.Id, -1, h.Due);
            return "";
        }

        // ------------------------------------------------------------------------------------------------ automation

        /// <summary>Assigns unassigned work to the best-suited person with the lightest queue (cheaper people for paperwork,
        /// partners for prep) and books a lawyer for every hearing without one. all = false: the clerk only (filings).</summary>
        public int AutoAssign(bool all, bool filingsOnly = false)
        {
            int n = 0;
            var load = new Dictionary<int, double>();
            foreach (var s in S.Staff) load[s.Id] = QueuedHours(s);
            var order = new List<MatterState>();
            foreach (var m in S.Matters) if (m.Status == MatterStatus.Open) order.Add(m);
            order.Sort((x, y) => { int a = x.HearingTask.Due.CompareTo(y.HearingTask.Due); return a != 0 ? a : x.Id.CompareTo(y.Id); });
            foreach (var m in order)
            {
                for (int i = 0; i < m.Tasks.Count; i++)
                {
                    var t = m.Tasks[i];
                    if (t.Finished || t.Staff != Attend.None) continue;
                    if (t.Kind == Kinds.Hearing)
                    {
                        if (!all) continue;
                        t.Staff = DefaultAttendee(t.Due, t.Slot);
                        if (t.Staff != Attend.None) n++;
                        continue;
                    }
                    if (filingsOnly && Kinds.Skill(t.Kind) != Kinds.File) continue;
                    StaffState best = null; double bs = 0;
                    foreach (var s in S.Staff)
                    {
                        if (filingsOnly && s.Role != "clerk") continue;
                        var r = RoleOf(s);
                        double sk = r.Skill(t.Kind);
                        if (sk <= 0) continue;
                        if (Kinds.Skill(t.Kind) != Kinds.Prep && r.Lawyer) sk *= 0.5;   // keep lawyers for prep and court
                        double score = sk / (1.0 + load[s.Id] / Math.Max(1, r.Hours));
                        if (score > bs) { bs = score; best = s; }
                    }
                    if (best == null) continue;
                    t.Staff = best.Id;
                    double rsk = RoleOf(best).Skill(t.Kind) * WorkMult(t.Kind);
                    load[best.Id] += rsk > 0 ? t.Left / rsk : 0;
                    n++;
                }
            }
            return n;
        }

        /// <summary>Every 10 game minutes: clerks pick up open filings; the Office Manager (automation) staffs everything.</summary>
        void AutoStaff()
        {
            if (S.Tutorial) return;
            if (Perk("automation")) AutoAssign(true);
            else if (HasRole("clerk")) AutoAssign(false, true);
        }

        // ------------------------------------------------------------------------------------------------ prep table

        public bool PrepOpen(MatterState m)
        {
            if (m == null || m.Status != MatterStatus.Open || m.PuzzleSolved) return false;
            var ev = m.Task(Kinds.Evidence);
            return ev == null || ev.Finished;
        }

        /// <summary>Matches an exhibit card to a claim. "" = a match; "wrong" = not this one (no penalty, try again).</summary>
        public string PrepMatch(int matterId, int claim, int exhibit)
        {
            var m = MatterById(matterId);
            if (m == null || m.Status != MatterStatus.Open) return "invalid";
            if (!PrepOpen(m)) return m.PuzzleSolved ? "done" : "locked";
            if (claim < 0 || claim >= m.Claims || exhibit < 0 || exhibit > m.Claims) return "invalid";
            if (m.Matched.Contains(claim)) return "done";
            if (exhibit != claim) return "wrong";
            m.Matched.Add(claim);
            Emit("prep_match", m.Id, claim, m.Matched.Count);
            if (m.PuzzleSolved) Emit("prep_done", m.Id);
            return "";
        }

        public double PuzzleFrac(MatterState m)
        {
            double f = m.Claims <= 0 ? 1 : (double)m.Matched.Count / m.Claims;
            if (f < 1 && m.Matched.Count == 0 && HasRole("paralegal")) f = Gd.PuzzleFallback;   // the paralegal sorts the exhibits for you
            return Math.Min(1, f);
        }

        // ------------------------------------------------------------------------------------------------ odds

        /// <summary>Win chance at the hearing. withTwist = false: what the card shows before the twist is known.</summary>
        public double Odds(MatterState m, int objection = 0, bool withTwist = true)
        {
            var tier = TierOf(m);
            var j = JudgeOf(m);
            var h = m.HearingTask;
            double p = Gd.OddsBase;
            var prep = m.Task(Kinds.Prep);
            p += Gd.OddsPrep * (prep == null ? 1 : prep.Frac);
            p += Gd.OddsPuzzle * PuzzleFrac(m);
            if (h != null)
            {
                if (h.Staff == Attend.Visiting) p += Gd.VisitingTrial;
                else { var s = h.Staff >= 0 ? StaffById(h.Staff) : null; p += s != null && RoleOf(s).Lawyer ? RoleOf(s).Trial : -Gd.NoLawyerPenalty; }
            }
            p -= m.NoLawyer;
            var file = m.Task(Kinds.File);
            bool late = m.Tasks.Exists(t => t.Late);
            switch (j.Kind)
            {
                case "small_claimant": if (m.Area == "traffic" || m.Area == "small_claims") p += j.Mod; break;
                case "peacemaker": p -= j.Mod; break;
                case "strict": p += late ? -j.Mod : 0.03; break;
                case "friday": p += WeekdayOf(h != null ? h.Due : S.Day) == 4 ? -j.Mod : 0.02; break;
            }
            p += Gd.OddsStanding * AreaStanding(m.Area);
            p -= tier.Diff;
            if (withTwist || m.Revealed) p -= m.Twist;
            if (file != null && !file.Finished) p -= Gd.OddsFileMissing;
            var ev = m.Task(Kinds.Evidence);
            if (ev != null && !ev.Finished) p -= Gd.OddsEvidenceMissing;
            foreach (var t in m.Tasks) if (t.Kind == Kinds.Motion && !t.Finished) p -= Gd.OddsFileMissing * 0.5;
            if (objection == 1) p += Gd.OddsObjection;
            else if (objection == 2) p -= Gd.OddsObjection;
            p += RoomOdds;
            return Math.Max(Gd.OddsMin, Math.Min(Gd.OddsMax, p));
        }

        /// <summary>The odds the matter card shows (twist hidden until evidence / case preview reveals it).</summary>
        public double ShownOdds(MatterState m) => Odds(m, 0, false);

        public double WinFee(MatterState m)
        {
            var a = AreaOf(m);
            double f = m.Fee == Fees.Contingency ? a.Value * Gd.ContingencyShare : a.Success;
            return Math.Round(f * TierOf(m).Fee * FeeMult(m.Area));
        }

        public double LossFee(MatterState m)
        {
            var a = AreaOf(m);
            double f = m.Fee == Fees.Contingency ? a.Value * Gd.LessonValueShare : a.Success * Gd.LessonShare;
            return Math.Round(f * TierOf(m).Fee * FeeMult(m.Area));
        }

        public double SettleFee(MatterState m) => Math.Round(WinFee(m) * Gd.SettleFactor * JudgeOf(m).Settle * SettleRoomMult);

        // ------------------------------------------------------------------------------------------------ settle / hearings

        public string Settle(int matterId)
        {
            var m = MatterById(matterId);
            if (m == null || m.Status != MatterStatus.Open) return "invalid";
            double fee = SettleFee(m);
            EarnFee(m, fee);
            S.Today.Settles++;
            S.Settles++;
            AddStanding(m.Area, Gd.StSettle);
            Emit("settled", m.Id, -1, fee, m.Area);
            Close(m);
            return "";
        }

        void StartHearings()
        {
            for (int i = 0; i < S.Matters.Count; i++)
            {
                var m = S.Matters[i];
                if (m.Status != MatterStatus.Open) continue;
                var h = m.HearingTask;
                if (h == null || h.Due > S.Day) continue;
                int slotMin = Gd.HearingSlots[Math.Min(h.Slot, Gd.HearingSlots.Length - 1)];
                if (h.Due == S.Day && S.Minute < slotMin) continue;
                StartHearing(m);
            }
        }

        void StartHearing(MatterState m)
        {
            var h = m.HearingTask;
            var s = h.Staff >= 0 ? StaffById(h.Staff) : null;
            bool ok = h.Staff == Attend.Visiting || (s != null && RoleOf(s).Lawyer && !s.Sick && !InSession(s.Id));
            if (!ok)
            {
                S.Today.NoLawyer++;
                if (!m.Continued && !m.Tutorial)
                {
                    // the court moves it once by itself (a fee, a little Standing) — never a lost matter for an empty chair
                    h.Due = S.Day + Gd.ContinuanceDays;
                    m.Continued = true;
                    if (!S.Tutorial && S.Day > Gd.ForgivingDays) { S.Cash -= Gd.AutoContinuanceFee; S.Today.Sanctions += Gd.AutoContinuanceFee; }
                    AddStanding(m.Area, Gd.StContinuance);
                    S.Today.Continuances++;
                    Emit("continued", m.Id, 1, h.Due);
                    return;
                }
                if (m.Tutorial && FounderOrLawyer() != Attend.None) { h.Staff = FounderOrLawyer(); s = StaffById(h.Staff); }
                else
                {
                    m.NoLawyer = 0;   // the "no lawyer" penalty is in Odds (attendee check); keep the field for saves mid-session
                    AddStanding(m.Area, Gd.StNoLawyer);
                    h.Staff = Attend.None;
                    s = null;
                }
            }
            if (s != null)
            {
                double need = h.Need;
                double over = Math.Max(0, need - s.HoursLeft);
                s.HoursLeft = Math.Max(0, s.HoursLeft - need);
                s.HoursWorked += need;
                S.Today.HoursWorked += need;
                s.Fatigue = Math.Min(100, s.Fatigue + (need - over + over * Gd.FatOvertime) * Gd.FatPerHour * FatigueGainMult);
                BillHours(m, need * RoleOf(s).Rate);
            }
            m.Status = MatterStatus.Hearing;
            m.HearingEnd = S.Minute + Gd.HearingSessionMin;
            S.Today.Hearings++;
            Emit("hearing_start", m.Id, h.Staff, ShownOdds(m));
        }

        void EndSessions()
        {
            for (int i = S.Matters.Count - 1; i >= 0; i--)
            {
                if (i >= S.Matters.Count) continue;
                var m = S.Matters[i];
                if (m.Status == MatterStatus.Hearing && m.HearingEnd <= S.Minute) Resolve(m.Id, 0);
            }
        }

        /// <summary>The verdict (the hearing scene calls it when it ends, with the optional "objection!" tap: 0 none, 1 well
        /// timed, 2 badly timed; otherwise the session ends by itself). Returns 1 won, 0 lost, -1 invalid.</summary>
        public int Resolve(int matterId, int objection)
        {
            var m = MatterById(matterId);
            if (m == null || m.Status != MatterStatus.Hearing) return -1;
            double p = Odds(m, objection);
            bool win = m.Tutorial || S.Tutorial || Rng.Frand() < p;
            double fee = win ? WinFee(m) : LossFee(m);
            EarnFee(m, fee);
            if (win) { S.Today.Wins++; S.Wins++; AddStanding(m.Area, Gd.StWin); }
            else { S.Today.Losses++; S.Losses++; AddStanding(m.Area, Gd.StLoss); }
            Emit("verdict", m.Id, win ? 1 : 0, fee, m.Area);
            Close(m);
            return win ? 1 : 0;
        }

        /// <summary>Result fees: contingency awards pass through the trust account (the firm's share moves to operating,
        /// the rest goes to the client) — the money shown on the verdict card is only the firm's fee.</summary>
        void EarnFee(MatterState m, double fee)
        {
            if (fee <= 0) return;
            S.Wip += fee;
            S.Today.Fees += fee;
            S.CareerFees += fee;
        }

        void Close(MatterState m)
        {
            if (m.Trust > 0) Emit("trust_refund", m.Id, -1, m.Trust);
            m.Trust = 0;
            S.Matters.Remove(m);
            S.Closed++;
            if (S.TutorialMatter == m.Id) S.Seen.Add("tut_matter_closed");
        }

        void AddStanding(string area, double d)
        {
            int i = Gd.AreaIndex(area);
            if (i < 0 || i >= S.Standing.Length) return;
            if (d < 0 && (S.Tutorial || S.Day <= Gd.ForgivingDays)) return;   // the guide and the first days never lower Standing
            S.Standing[i] = Math.Max(0, Math.Min(Gd.StMax, S.Standing[i] + d));
        }

        // ------------------------------------------------------------------------------------------------ the guide's script

        /// <summary>The guide's first client: a parking ticket at reception (idempotent; also used by "Replay the guide").</summary>
        public MatterState SeedTutorial()
        {
            var cur = S.TutorialMatter > 0 ? MatterById(S.TutorialMatter) : null;
            if (cur != null) return cur;
            var m = new MatterState
            {
                Id = S.NextId++, Area = Gd.Areas[0].Id, Tier = "S", Fee = Gd.Areas[0].Fee, Status = MatterStatus.Intake, Title = 0, Client = 0,
                Opponent = Gd.Parties.Count - 1, Judge = 0, Twist = 0, Revealed = true, Tutorial = true, Claims = 2, Exhibits = new List<int> { 1, 2, 0 },
                Expires = Gd.DayEnd,
            };
            S.Matters.Add(m);
            S.TutorialMatter = m.Id;
            S.Seen.Remove("tut_matter_closed");
            Emit("walkin", m.Id);
            return m;
        }

        /// <summary>The guide's last step: two hearings at the same court slot, one lawyer (idempotent).</summary>
        public void SpawnTutorialCollision()
        {
            if (S.TutorialCollision.Count > 0 && S.TutorialCollision.TrueForAll(id => MatterById(id) != null)) return;
            S.TutorialCollision.Clear();
            int day = S.Day + 2, slot = 0;
            int lawyer = FounderOrLawyer();
            for (int k = 0; k < 2; k++)
            {
                var m = Generate();
                m.Area = Gd.Areas[k % Math.Min(2, Gd.Areas.Count)].Id;
                m.Fee = AreaOf(m).Fee;
                m.Tier = "S";
                m.Status = MatterStatus.Intake;
                m.Claims = AreaOf(m).Claims;
                m.Title = k % AreaOf(m).Titles;
                S.Matters.Add(m);
                Accept(m.Id);
                var h = m.HearingTask;
                h.Due = day;
                h.Slot = slot;
                h.Staff = lawyer;
                foreach (var t in m.Tasks) if (t.Kind != Kinds.Hearing) t.Due = Math.Min(t.Due, day);
                S.TutorialCollision.Add(m.Id);
            }
            S.Today.Accepted -= 2;   // scripted, not the player's intake
        }

        /// <summary>The guide finished or was skipped: crises, sanctions and real verdicts may start (after the free days);
        /// the scripted client becomes an ordinary one.</summary>
        public void EndTutorial()
        {
            S.Tutorial = false;
            var m = S.TutorialMatter > 0 ? MatterById(S.TutorialMatter) : null;
            if (m != null) m.Tutorial = false;
        }

        /// <summary>The guide (re)starts: safe mode. fresh = from step 1 (first launch or "Replay the guide"): the scripted
        /// client waits at reception and the collision script is reset.</summary>
        public void BeginTutorial(bool fresh)
        {
            S.Tutorial = true;
            if (!fresh) return;
            var m = S.TutorialMatter > 0 ? MatterById(S.TutorialMatter) : null;
            if (m != null && m.Tutorial) return;
            S.TutorialMatter = 0;
            S.TutorialCollision.Clear();
            SeedTutorial();
        }
    }
}
