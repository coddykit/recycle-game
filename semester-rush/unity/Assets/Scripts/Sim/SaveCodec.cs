// Semester Rush sim core — save codec (fleet §14). Encode = canonical JSON of the whole career (a live day included:
// trucks mid-road, a jammed machine, the report screen, the guide). Decode never throws and never touches a running game;
// it accepts every state the sim can produce (negative cash after a bad day, a waiting truck, an empty yard) and refuses
// only what the sim can never write (unknown nodes / vehicle types, indices out of range, another game's file).
// Keeping unreadable files (.unreadable copies, .bak fallback) is CoddyKit.SaveRepository's job.
using System;
using System.Collections.Generic;

namespace SemesterRush.Sim
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
            if (J.S(o, "game") != "semester-rush") return Fail("wrong_game");
            int v = J.I(o, "v", -1);
            if (v < 1 || v > State.Version) return Fail("version");
            var s = new State
            {
                Seed = J.L(o, "seed", 1), RngState = J.L(o, "rng", 1), Day = J.I(o, "day", 1), Minute = J.D(o, "minute", gd.DayStart),
                Phase = J.S(o, "phase", Phases.Plan), Speed = J.I(o, "speed", 1), Paused = J.B(o, "paused"), Cash = J.D(o, "cash"),
                Rating = J.D(o, "rating", 50), Drivers = J.I(o, "drivers", 1), Techs = J.I(o, "techs"), Researching = J.S(o, "researching"),
                ResearchLeftS = J.D(o, "research_left"), NextDispatchAt = J.D(o, "next_dispatch", gd.DayStart), Tutorial = J.B(o, "tutorial"),
                FuelVoucher = J.B(o, "voucher"), OfflinePending = J.D(o, "offline_pending"), LastSeen = J.L(o, "last_seen"),
                BestDayStars = J.I(o, "best_stars"), ThreeStarDays = J.I(o, "three_star_days"), Containers = J.L(o, "containers"), Bales = J.I(o, "bales"),
            };
            if (!Finite(s.Minute) || !Finite(s.Cash) || !Finite(s.Rating) || !Finite(s.ResearchLeftS) || !Finite(s.OfflinePending)) return Fail("not_finite");
            if (s.Day < 1 || s.Day > 1000000) return Fail("day");
            if (!Phases.Valid(s.Phase)) return Fail("phase");
            if (s.Minute < gd.DayStart - 1 || s.Minute > gd.DayEnd + 1) return Fail("minute");
            if (s.Drivers < 0 || s.Techs < 0 || s.Speed < 1 || s.Speed > 8) return Fail("counts");
            s.Districts = J.StrList(o, "districts");
            foreach (var d in s.Districts) if (gd.District(d) == null) return Fail("district");
            if (s.Districts.Count == 0) return Fail("district");
            s.Research = J.StrList(o, "research");
            foreach (var r in s.Research) if (!gd.ResearchById.ContainsKey(r)) return Fail("research");
            if (s.Researching != "" && !gd.ResearchById.ContainsKey(s.Researching)) return Fail("research");
            s.Seen = J.StrList(o, "seen");

            foreach (var p in J.Arr(o, "points") ?? new List<object>())
            {
                var ps = PointState.Read(p);
                if (ps.Node < 0 || ps.Node >= gd.Nodes.Count || gd.Nodes[ps.Node].Kind != "site") return Fail("point_node");
                if (!gd.PointKinds.ContainsKey(ps.Kind)) return Fail("point_kind");
                s.Points.Add(ps);
            }
            foreach (var t in J.Arr(o, "trucks") ?? new List<object>())
            {
                var ts = TruckState.Read(t);
                if (!gd.Vehicles.ContainsKey(ts.Type)) return Fail("truck_type");
                if (ts.AtNode < 0 || ts.AtNode >= gd.Nodes.Count) return Fail("truck_node");
                foreach (int n in ts.Path) if (n < 0 || n >= gd.Nodes.Count) return Fail("truck_path");
                foreach (int i in ts.Stops) if (i < 0 || i >= s.Points.Count) return Fail("truck_stop");
                if (ts.Target < -1 || ts.Target >= s.Points.Count) return Fail("truck_target");
                if (!Finite(ts.PathPos) || !Finite(ts.PathKm) || !Finite(ts.TimeLeft)) return Fail("not_finite");
                s.Trucks.Add(ts);
            }
            foreach (var j in J.Arr(o, "tech_jobs") ?? new List<object>()) s.TechJobs.Add(TechJob.Read(j));
            s.Depot = DepotState.Read(J.Obj(o, "depot"), gd);
            var mk = JW.Dbl(o, "market", Mats.N);
            for (int m = 0; m < Mats.N; m++) s.Market[m] = mk[m] > 0 ? mk[m] : 1;
            foreach (var e in J.Arr(o, "events") ?? new List<object>()) s.Events.Add(ActiveEvent.Read(e));
            foreach (var e in J.Arr(o, "planned") ?? new List<object>()) s.Planned.Add(PlannedEvent.Read(e));
            s.Today = DayStats.Read(J.Obj(o, "today"));
            foreach (var h in J.Arr(o, "history") ?? new List<object>()) s.History.Add(DayStats.Read(h));
            if (J.Obj(o, "rewards") is JObj rw) foreach (var kv in rw) s.Rewards[kv.Key] = RewardUse.Read(kv.Value);
            return new DecodeResult { Ok = true, Sim = new Simulation(gd, s) };
        }

        static bool Finite(double d) => !double.IsNaN(d) && !double.IsInfinity(d);
    }
}
