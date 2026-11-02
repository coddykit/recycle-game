// Volt City sim core — engine independent (no UnityEngine). C# 9 / .NET Standard 2.1.
using System;
using System.Collections.Generic;

namespace VoltCity.Sim
{
    public sealed class DecodeResult
    {
        public bool Ok;
        public string Error = "";
        public Simulation Sim;
    }

    /// <summary>
    /// Save codec (CLAUDE section 14). Encode = canonical JSON text of the whole career (a live shift included: its plan +
    /// grid snapshot + command log). Decode never throws and never touches a running game; it accepts every state the sim
    /// can produce (negative cash after a bad shift, mid-ramp, mid-repair, a crew on the road, capacity+1 overheat, the
    /// purchase moment, the report screen, a pending tap) and refuses only what the sim can never write.
    /// An unreadable save is the shared save repository's job (keep .unreadable copies, load .bak) — the codec just says no.
    /// </summary>
    public static class SaveCodec
    {
        public const double MaxNumber = 9007199254740991.0;

        public static string Encode(Simulation sim) => Json.Write(sim.Snapshot());

        public static DecodeResult Decode(string text, GameData gd)
        {
            object raw = Json.TryParse(text);
            if (raw == null) return Fail("invalid_json");
            try { return Decode(raw, gd); }
            catch (Exception e) { return Fail("decode_exception_" + e.GetType().Name); }
        }

        static DecodeResult Fail(string e) => new DecodeResult { Error = e };

        static bool Int(object o, string key, long lo, long hi, out long v)
        {
            v = 0;
            var x = J.Get(o, key);
            if (!J.IsInt(x)) return false;
            v = J.L(x);
            return v >= lo && v <= hi;
        }

        public static DecodeResult Decode(object raw, GameData gd)
        {
            if (!(raw is JObj d)) return Fail("invalid_root");
            if (!J.IsInt(J.Get(d, "schema")) || J.L(d, "schema") < 1 || J.L(d, "schema") > CareerState.Schema) return Fail("unsupported_schema");
            foreach (var k in new[] { "seed", "clock", "acc_us", "phase", "cash", "shift_no", "chapter", "chapter_shift", "rating_x100", "rating_hist", "owned",
                "plants", "lines", "ties", "crews", "crew_speed", "tree_trim", "dr", "weather_station", "auto", "auto_on", "hooked", "rush", "carry", "wx_prev",
                "heat_left", "heat_pct", "last_stars", "last_net", "plan", "shift", "report", "reports", "pending", "log", "speed", "paused", "tutorial_safe",
                "ch5_good", "ch5_done", "stats", "wow", "rewards", "receipts", "last_seen", "offline_last" })
                if (!d.ContainsKey(k)) return Fail("missing_" + k);
            var s = new CareerState();
            long v;
            if (!Int(d, "seed", 1, 2147483646, out v)) return Fail("invalid_seed"); s.Seed = v;
            if (!J.IsNum(J.Get(d, "clock")) || J.D(d, "clock") < 0) return Fail("invalid_clock"); s.Clock = J.D(d, "clock");
            if (!Int(d, "acc_us", 0, 1L << 40, out v)) return Fail("invalid_acc_us"); s.AccUs = v;
            s.Phase = J.S(d, "phase");
            if (Array.IndexOf(CareerState.Phases, s.Phase) < 0) return Fail("invalid_phase");
            if (!Int(d, "cash", -(1L << 52), 1L << 52, out v)) return Fail("invalid_cash"); s.Cash = v;
            if (!Int(d, "shift_no", 0, 1 << 30, out v)) return Fail("invalid_shift_no"); s.ShiftNo = (int)v;
            if (!Int(d, "chapter", 1, 5, out v)) return Fail("invalid_chapter"); s.Chapter = (int)v;
            if (!Int(d, "chapter_shift", 0, 1 << 30, out v)) return Fail("invalid_chapter_shift"); s.ChapterShift = (int)v;
            if (!Int(d, "rating_x100", 0, 500, out v)) return Fail("invalid_rating"); s.RatingX100 = (int)v;
            if (!Int(d, "crews", 1, 5, out v)) return Fail("invalid_crews"); s.Crews = (int)v;
            if (!Int(d, "crew_speed", 0, 2, out v)) return Fail("invalid_crew_speed"); s.CrewSpeed = (int)v;
            if (!Int(d, "tree_trim", 0, 2, out v)) return Fail("invalid_tree_trim"); s.TreeTrim = (int)v;
            if (!Int(d, "dr", 0, 2, out v)) return Fail("invalid_dr"); s.Dr = (int)v;
            if (!Int(d, "weather_station", 0, 1, out v)) return Fail("invalid_weather_station"); s.WeatherStation = (int)v;
            if (!Int(d, "heat_left", 0, 3, out v)) return Fail("invalid_heat_left"); s.HeatLeft = (int)v;
            if (!Int(d, "heat_pct", 0, 100, out v)) return Fail("invalid_heat_pct"); s.HeatPct = (int)v;
            if (!Int(d, "last_stars", 1, 3, out v)) return Fail("invalid_last_stars"); s.LastStars = (int)v;
            if (!Int(d, "last_net", -(1L << 52), 1L << 52, out v)) return Fail("invalid_last_net"); s.LastNet = v;
            if (!Int(d, "speed", 1, 4, out v)) return Fail("invalid_speed"); s.Speed = (int)v;
            if (!Int(d, "ch5_good", 0, 1 << 30, out v)) return Fail("invalid_ch5"); s.Ch5Good = (int)v;
            if (!Int(d, "ch5_done", -1, 1 << 30, out v)) return Fail("invalid_ch5"); s.Ch5Done = (int)v;
            if (!Int(d, "last_seen", 0, 1L << 40, out v)) return Fail("invalid_last_seen"); s.LastSeen = v;
            if (!(J.Get(d, "paused") is bool) || !(J.Get(d, "tutorial_safe") is bool)) return Fail("invalid_flags");
            s.Paused = J.B(d, "paused"); s.TutorialSafe = J.B(d, "tutorial_safe");
            s.WxPrev = J.S(d, "wx_prev");
            if (s.WxPrev != "" && !gd.WeatherById.ContainsKey(s.WxPrev)) return Fail("invalid_wx_prev");
            var hist = J.Arr(d, "rating_hist");
            if (hist == null || hist.Count > gd.RatingWindow) return Fail("invalid_rating_hist");
            foreach (var h in hist) { if (!J.IsInt(h) || J.L(h) < 0 || J.L(h) > 500) return Fail("invalid_rating_hist"); s.RatingHist.Add(J.I(h)); }
            var owned = J.Obj(d, "owned");
            if (owned == null) return Fail("invalid_owned");
            foreach (var k in owned.Keys) { if (!gd.Investment.ContainsKey(k) || !J.IsInt(owned[k])) return Fail("invalid_owned"); s.Owned[k] = J.I(owned[k]); }
            var plants = J.Obj(d, "plants");
            if (plants == null) return Fail("invalid_plants");
            foreach (var k in plants.Keys) { if (!gd.Plant.ContainsKey(k) || !J.IsInt(plants[k]) || J.L(plants[k]) < 0 || J.L(plants[k]) > 3) return Fail("invalid_plants"); s.Plants[k] = J.I(plants[k]); }
            var lines = J.Obj(d, "lines");
            if (lines == null) return Fail("invalid_lines");
            foreach (var k in lines.Keys) { if (!gd.Corridor.ContainsKey(k) || !J.IsInt(lines[k]) || J.L(lines[k]) < 0 || J.L(lines[k]) > 2) return Fail("invalid_lines"); s.Lines[k] = J.I(lines[k]); }
            var ties = J.Arr(d, "ties");
            if (ties == null) return Fail("invalid_ties");
            foreach (var t in ties) { if (!gd.Tie.ContainsKey(J.Str(t))) return Fail("invalid_ties"); s.Ties.Add(J.Str(t)); }
            var auto = J.Arr(d, "auto");
            if (auto == null) return Fail("invalid_auto");
            foreach (var a in auto) s.Auto.Add(J.Str(a));
            var autoOn = J.Obj(d, "auto_on");
            if (autoOn == null) return Fail("invalid_auto_on");
            foreach (var k in autoOn.Keys) s.AutoOn[k] = J.B(autoOn[k], true);
            var hooked = J.Obj(d, "hooked");
            if (hooked == null || hooked.Count < 1) return Fail("invalid_hooked");
            foreach (var k in hooked.Keys) { if (!gd.District.ContainsKey(k) || !J.IsInt(hooked[k]) || J.L(hooked[k]) < 0) return Fail("invalid_hooked"); s.Hooked[k] = J.I(hooked[k]); }
            foreach (var r in J.StrList(d, "rush")) { if (!gd.District.ContainsKey(r)) return Fail("invalid_rush"); s.Rush.Add(r); }
            var carry = J.Obj(d, "carry");
            if (carry == null) return Fail("invalid_carry");
            s.Carry = Carry.FromJson(carry);
            if (s.Carry.BatSoc < 0 || s.Carry.ResWh < 0 || s.Carry.BatMode < 0 || s.Carry.BatMode > 3) return Fail("invalid_carry");
            foreach (var kv in s.Carry.Steps) if (kv.Value < 0 || kv.Value > 10 || gd.PlantIndex(kv.Key) < 0) return Fail("invalid_carry");
            var planObj = J.Obj(d, "plan");
            if (planObj != null && planObj.Count > 0)
            {
                s.Plan = ShiftPlan.FromJson(planObj, gd, out string perr);
                if (s.Plan == null) return Fail(perr);
            }
            var rep = J.Obj(d, "report");
            if (rep != null && rep.Count > 0) s.Report = ShiftReport.FromJson(rep);
            if (s.Phase == "report" && s.Report == null) return Fail("invalid_report");
            var reps = J.Arr(d, "reports");
            if (reps == null || reps.Count > 64) return Fail("invalid_reports");
            foreach (var r in reps) { var sr = ShiftReport.FromJson(r); if (sr == null) return Fail("invalid_reports"); s.Reports.Add(sr); }
            var pend = J.Arr(d, "pending");
            if (pend == null || pend.Count > 256) return Fail("invalid_pending");
            foreach (var c in pend) { var cmd = Cmd.FromJson(c); if (cmd == null) return Fail("invalid_pending"); s.Pending.Add(cmd); }
            var log = J.Arr(d, "log");
            if (log == null || log.Count > 100000) return Fail("invalid_log");
            foreach (var e in log)
            {
                var cmd = Cmd.FromJson(J.Get(e, "cmd"));
                if (cmd == null || !J.IsInt(J.Get(e, "step"))) return Fail("invalid_log");
                s.Log.Add(new KeyValuePair<int, Cmd>(J.I(e, "step"), cmd));
            }
            var stats = J.Obj(d, "stats");
            if (stats == null) return Fail("invalid_stats");
            s.Stats = Stats.FromJson(stats);
            var wow = J.Obj(d, "wow");
            if (wow == null) return Fail("invalid_wow");
            foreach (var k in wow.Keys) s.Wow[k] = J.I(wow[k]);
            var rw = J.Obj(d, "rewards");
            if (rw == null) return Fail("invalid_rewards");
            s.Rewards = RewardState.FromJson(rw);
            var rc = J.Arr(d, "receipts");
            if (rc == null || rc.Count > gd.ReceiptsMax) return Fail("invalid_receipts");
            foreach (var x in rc) { string h = J.Str(x); if (h.Length != 64) return Fail("invalid_receipts"); s.Receipts.Add(h); }
            s.OfflineLast = OfflineSummary.FromJson(J.Get(d, "offline_last"));
            var sim = new Simulation(gd, 1) { S = s };
            if (s.Phase == "live")
            {
                var shift = J.Obj(d, "shift");
                if (s.Plan == null || shift == null || shift.Count == 0) return Fail("invalid_live_shift");
                var g = new Grid();
                string err = g.RestoreFrom(gd, s.Plan, sim.Ctx(), shift);
                if (err != "") return Fail(err);
                sim.Grid = g;
            }
            return new DecodeResult { Ok = true, Sim = sim };
        }
    }
}
