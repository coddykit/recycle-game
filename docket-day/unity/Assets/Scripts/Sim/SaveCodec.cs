// Docket Day sim core — save codec (fleet §14). Encode = canonical JSON of the whole career (a live day included: a
// hearing in session, a double-booked partner, the report screen with an unbilled day, the guide half way). Decode never
// throws and never touches a running game; it accepts every state the sim can produce (negative cash after a bad day, a
// late filing, a task worked past its need, a sick intern, a matter on a continuance) and refuses only what the sim can
// never write (unknown roles / areas / rooms, ids out of range, a non-finite number, another game's file).
// Keeping unreadable files (.unreadable copies, .bak fallback) is CoddyKit.SaveRepository's job.
using System;
using System.Collections.Generic;

namespace DocketDay.Sim
{
    public sealed class DecodeResult
    {
        public bool Ok;
        public string Error = "";
        public Simulation Sim;
    }

    public static class SaveCodec
    {
        public static string Encode(Simulation sim) => Json.Write(sim.Snapshot());

        public static DecodeResult Decode(string text, GameData gd)
        {
            if (gd == null || !gd.Ok) return Fail("no_data");
            object raw = Json.TryParse(text);
            if (!(raw is JObj o)) return Fail("invalid_json");
            try { return Decode(o, gd); }
            catch (Exception e) { return Fail("decode_exception_" + e.GetType().Name); }
        }

        static DecodeResult Fail(string e) => new DecodeResult { Error = e };

        static DecodeResult Decode(JObj o, GameData gd)
        {
            if (J.S(o, "game") != "docket-day") return Fail("wrong_game");
            int v = J.I(o, "v", -1);
            if (v < 1 || v > State.Version) return Fail("version");
            var s = new State
            {
                Seed = J.L(o, "seed", 1), RngState = J.L(o, "rng", 1), Day = J.I(o, "day", 1), Minute = J.D(o, "minute", gd.DayStart),
                Phase = J.S(o, "phase", Phases.Plan), Speed = J.I(o, "speed", 1), Paused = J.B(o, "paused"), Cash = J.D(o, "cash"), Wip = J.D(o, "wip"),
                Billed = J.B(o, "billed"), Boosted = J.B(o, "boosted"), Chapter = J.I(o, "chapter", 1), NextId = J.I(o, "next_id", 1), NextStaff = J.I(o, "next_staff"),
                Building = J.S(o, "building"), Tutorial = J.B(o, "tutorial"), TutorialMatter = J.I(o, "tut_matter"), OfflinePending = J.D(o, "offline_pending"),
                LastSeen = J.L(o, "last_seen"), Closed = J.I(o, "closed"), Wins = J.I(o, "wins"), Losses = J.I(o, "losses"), Settles = J.I(o, "settles"),
                BestDayStars = J.I(o, "best_stars"), CareerFees = J.D(o, "career_fees"),
            };
            if (!Finite(s.Minute) || !Finite(s.Cash) || !Finite(s.Wip) || !Finite(s.OfflinePending) || !Finite(s.CareerFees)) return Fail("not_finite");
            if (s.Day < 1 || s.Day > 1000000) return Fail("day");
            if (!Phases.Valid(s.Phase)) return Fail("phase");
            if (s.Minute < gd.DayStart - 1 || s.Minute > gd.DayEnd + 1) return Fail("minute");
            if (s.Speed < 1 || s.Speed > 8) return Fail("speed");
            if (s.Chapter < 1 || gd.ChapterOf(s.Chapter) == null) return Fail("chapter");
            if (s.NextId < 1 || s.NextStaff < 0) return Fail("ids");
            if (s.Building != "" && gd.Room(s.Building) == null) return Fail("room");

            var st = JW.Dbls(o, "standing");
            s.Standing = new double[gd.Areas.Count];
            for (int i = 0; i < s.Standing.Length; i++) s.Standing[i] = i < st.Count ? Math.Max(0, Math.Min(gd.StMax, st[i])) : gd.StartStanding;

            var staffIds = new HashSet<int>();
            foreach (var x in J.Arr(o, "staff") ?? new List<object>())
            {
                var ss = StaffState.Read(x);
                if (gd.Role(ss.Role) == null) return Fail("staff_role");
                if (ss.Id < 0 || ss.Id >= s.NextStaff || !staffIds.Add(ss.Id)) return Fail("staff_id");
                s.Staff.Add(ss);
            }
            if (s.Staff.Count == 0) return Fail("no_staff");

            var matterIds = new HashSet<int>();
            foreach (var x in J.Arr(o, "matters") ?? new List<object>())
            {
                var m = MatterState.Read(x);
                if (m.Id < 1 || m.Id >= s.NextId || !matterIds.Add(m.Id)) return Fail("matter_id");
                if (!MatterStatus.Valid(m.Status)) return Fail("matter_status");
                if (gd.Area(m.Area) == null) return Fail("matter_area");
                if (gd.Tier(m.Tier) == null) return Fail("matter_tier");
                if (!Fees.Valid(m.Fee)) return Fail("matter_fee");
                if (m.Judge < 0 || m.Judge >= gd.Judges.Count) return Fail("matter_judge");
                if (m.Client < 0 || m.Client >= gd.Parties.Count || m.Opponent < 0 || m.Opponent >= gd.Parties.Count) return Fail("matter_party");
                if (m.Claims < 1 || m.Claims > 6) return Fail("matter_claims");
                foreach (int e in m.Exhibits) if (e < 0 || e > m.Claims) return Fail("matter_exhibit");
                foreach (int c in m.Matched) if (c < 0 || c >= m.Claims) return Fail("matter_matched");
                foreach (var t in m.Tasks)
                {
                    if (!Kinds.Valid(t.Kind)) return Fail("task_kind");
                    if (t.Slot < 0 || t.Slot >= gd.HearingSlots.Length) return Fail("task_slot");
                    if (t.Staff < Attend.Visiting) return Fail("task_staff");
                    if (t.Staff >= 0 && !staffIds.Contains(t.Staff)) t.Staff = Attend.None;   // a fired / day-pass person: the task is simply unassigned
                    if (!Finite(t.Need) || !Finite(t.Done)) return Fail("not_finite");
                }
                if (m.Active && m.HearingTask == null) return Fail("matter_hearing");
                s.Matters.Add(m);
            }
            foreach (var x in J.Arr(o, "rooms") ?? new List<object>())
            {
                var r = RoomState.Read(x);
                if (gd.Room(r.Id) == null) return Fail("room");
                s.Rooms.Add(r);
            }
            s.Rules = J.StrList(o, "rules") ?? new List<string>();
            foreach (var r in s.Rules) if (gd.Area(r) == null) return Fail("rule");
            foreach (var e in J.Arr(o, "events") ?? new List<object>()) s.Events.Add(ActiveEvent.Read(e));
            foreach (var e in J.Arr(o, "planned") ?? new List<object>()) s.Planned.Add(PlannedEvent.Read(e));
            s.Today = DayStats.Read(J.Obj(o, "today"));
            foreach (var h in J.Arr(o, "history") ?? new List<object>()) s.History.Add(DayStats.Read(h));
            s.TutorialCollision = JW.Ints(o, "tut_collision");
            if (J.Obj(o, "rewards") is JObj rw) foreach (var kv in rw) s.Rewards[kv.Key] = RewardUse.Read(kv.Value);
            s.Seen = J.StrList(o, "seen") ?? new List<string>();
            return new DecodeResult { Ok = true, Sim = new Simulation(gd, s) };
        }

        static bool Finite(double d) => !double.IsNaN(d) && !double.IsInfinity(d);
    }
}
