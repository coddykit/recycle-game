// Charge Nurse sim core — save codec (fleet §14). Encode = canonical JSON of the whole career (a live shift included:
// patients mid-corridor, a scanner down, a closed ward, the report screen, the guide). Decode never throws and never
// touches a running game; it accepts every state the sim can produce (negative cash after a bad day, a patient boarding
// in the OR, a bed reserved for a porter) and refuses only what the sim can never write (unknown zones / rooms / stages,
// indices out of range, another game's file). Keeping unreadable files (.unreadable copies, .bak fallback) is
// CoddyKit.SaveRepository's job.
using System;
using System.Collections.Generic;

namespace ChargeNurse.Sim
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
            if (J.S(o, "game") != "charge-nurse") return Fail("wrong_game");
            int v = J.I(o, "v", -1);
            if (v < 1 || v > State.Version) return Fail("version");
            var s = new State
            {
                Seed = J.L(o, "seed", 1), RngState = J.L(o, "rng", 1), Day = J.I(o, "day", 1), Minute = J.D(o, "minute", gd.DayStart),
                Phase = J.S(o, "phase", Phases.Plan), Speed = J.I(o, "speed", 1), Paused = J.B(o, "paused"), Cash = J.D(o, "cash"),
                Reputation = J.D(o, "reputation", 50), Burnout = J.D(o, "burnout"), Researching = J.S(o, "researching"), ResearchLeft = J.D(o, "research_left"),
                NextPatient = J.I(o, "next_patient", 1), Queue = J.S(o, "queue", QueueRules.Balanced), DivertAt = J.I(o, "divert_at"),
                HandoverDone = J.B(o, "handover"), Surge = J.B(o, "surge"), SickTeam = J.S(o, "sick"), FloatTeam = J.S(o, "float_team"), FloatLeft = J.D(o, "float_left"),
                NeighborUsed = J.B(o, "neighbor"), Tutorial = J.B(o, "tutorial"), ArrivalsOn = J.B(o, "arrivals_on", true),
                OfflinePending = J.D(o, "offline_pending"), LastSeen = J.L(o, "last_seen"), BestStars = J.I(o, "best_stars"), GoodDays = J.I(o, "good_days"),
                CareerTreated = J.L(o, "career_treated"),
            };
            if (!Finite(s.Minute) || !Finite(s.Cash) || !Finite(s.Reputation) || !Finite(s.ResearchLeft) || !Finite(s.OfflinePending) || !Finite(s.Burnout) || !Finite(s.FloatLeft))
                return Fail("not_finite");
            if (s.Day < 1 || s.Day > 1000000) return Fail("day");
            if (!Phases.Valid(s.Phase)) return Fail("phase");
            if (s.Minute < gd.DayStart - 1 || s.Minute > gd.DayEnd + 1) return Fail("minute");
            if (s.Speed < 1 || s.Speed > 8 || s.NextPatient < 1) return Fail("counts");
            if (!QueueRules.Valid(s.Queue)) return Fail("queue");
            foreach (var t in new[] { s.SickTeam, s.FloatTeam }) if (t != "" && Array.IndexOf(Teams.All, t) < 0) return Fail("team");

            if (J.Obj(o, "teams") is JObj tm)
                foreach (var kv in tm)
                {
                    if (Teams.Index(kv.Key) < 0) return Fail("team");
                    int n = J.I(kv.Value);
                    if (n < 0 || n > 99) return Fail("team_count");
                    s.Teams[kv.Key] = n;
                }
            foreach (var t in Teams.Pool) if (!s.Teams.ContainsKey(t)) s.Teams[t] = 0;
            if (!ReadNums(o, "fatigue", s.Fatigue) || !ReadNums(o, "busy_min", s.BusyMin) || !ReadNums(o, "cap_min", s.CapMin)) return Fail("team");

            s.Wings = J.StrList(o, "wings");
            if (s.Wings.Count == 0) return Fail("wing");
            foreach (var w in s.Wings) if (gd.Wing(w) == null) return Fail("wing");
            s.Research = J.StrList(o, "research");
            foreach (var r in s.Research) if (!gd.ResearchById.ContainsKey(r)) return Fail("research");
            if (s.Researching != "" && !gd.ResearchById.ContainsKey(s.Researching)) return Fail("research");
            s.Seen = J.StrList(o, "seen");

            foreach (var x in J.Arr(o, "beds") ?? new List<object>())
            {
                var b = Bed.Read(x);
                if (b.Zone < 0 || b.Zone >= gd.Zones.Count) return Fail("bed_zone");
                var z = gd.Zones[b.Zone];
                if (z.Kind != "bays" && z.Kind != "ward") return Fail("bed_zone");
                if (b.Bay != (z.Kind == "bays") || b.Slot < 0 || b.Slot >= Math.Max(1, z.Slots)) return Fail("bed_slot");
                if (!BedState.Valid(b.State) || !Finite(b.Left)) return Fail("bed_state");
                s.Beds.Add(b);
            }
            foreach (var x in J.Arr(o, "rooms") ?? new List<object>())
            {
                var r = Room.Read(x);
                if (!gd.Rooms.ContainsKey(r.Kind) || r.Zone < 0 || r.Zone >= gd.Zones.Count) return Fail("room");
                if (r.Techs < 0 || r.Techs > 99 || !Finite(r.Down)) return Fail("room");
                s.Rooms.Add(r);
            }
            var ids = new HashSet<int>();
            foreach (var x in J.Arr(o, "patients") ?? new List<object>())
            {
                var p = Patient.Read(x);
                if (!Stage.Valid(p.Stage)) return Fail("patient_stage");
                if (p.Sev < 0 || p.Sev > Sev.Red || p.Tag < -1 || p.Tag > Sev.Red) return Fail("patient_sev");
                if (p.Bed < -1 || p.Bed >= s.Beds.Count || p.Room < -1 || p.Room >= s.Rooms.Count) return Fail("patient_where");
                if (p.Dest >= s.Beds.Count || (p.Dest <= -2 && -2 - p.Dest >= s.Rooms.Count)) return Fail("patient_dest");
                if (p.Test != "" && p.Test != "xray" && p.Test != "lab" && p.Test != "ct") return Fail("patient_test");
                if (!Finite(p.Left) || !Finite(p.Wait) || !Finite(p.Total)) return Fail("not_finite");
                if (!ids.Add(p.Id) || p.Id >= s.NextPatient) return Fail("patient_id");
                s.Patients.Add(p);
            }
            if (J.Obj(o, "ambulance") is JObj am)
            {
                s.Ambulance = AmbulanceCall.Read(am);
                if (s.Ambulance.Sev < 0 || s.Ambulance.Sev > Sev.Red) return Fail("ambulance");
            }
            foreach (var e in J.Arr(o, "events") ?? new List<object>()) s.Events.Add(ActiveEvent.Read(e));
            foreach (var e in J.Arr(o, "planned") ?? new List<object>()) s.Planned.Add(PlannedEvent.Read(e));
            s.Today = DayStats.Read(J.Obj(o, "today"));
            foreach (var h in J.Arr(o, "history") ?? new List<object>()) s.History.Add(DayStats.Read(h));
            if (J.Obj(o, "rewards") is JObj rw) foreach (var kv in rw) s.Rewards[kv.Key] = RewardUse.Read(kv.Value);
            return new DecodeResult { Ok = true, Sim = new Simulation(gd, s) };
        }

        static bool ReadNums(JObj o, string key, Dictionary<string, double> into)
        {
            if (!(J.Obj(o, key) is JObj d)) return true;
            foreach (var kv in d)
            {
                if (Array.IndexOf(Teams.All, kv.Key) < 0) return false;
                double x = J.D(kv.Value);
                if (!Finite(x)) return false;
                into[kv.Key] = x;
            }
            return true;
        }

        static bool Finite(double d) => !double.IsNaN(d) && !double.IsInfinity(d);
    }
}
