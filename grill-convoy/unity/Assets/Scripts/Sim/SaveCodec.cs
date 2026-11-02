// Grill Convoy sim core — save codec (fleet §14). Encode = canonical JSON of the whole career (a live day included:
// trucks mid-road or mid-restock, a line at the window, a flare-up, the report screen, the guide's hand-cooked ticket).
// Decode never throws and never touches a running game; it accepts every state the sim can produce (negative cash after a
// bad day, an empty commissary, a truck with no pitch) and refuses only what the sim can never write (unknown nodes /
// truck types / stations / roles / cards, indices out of range, another game's file).
// Keeping unreadable files (.unreadable copies, .bak fallback) is CoddyKit.SaveRepository's job.
using System;
using System.Collections.Generic;

namespace GrillConvoy.Sim
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
            if (J.S(o, "game") != "grill-convoy") return Fail("wrong_game");
            int v = J.I(o, "v", -1);
            if (v < 1 || v > State.Version) return Fail("version");
            var s = new State
            {
                Seed = J.L(o, "seed", 1), RngState = J.L(o, "rng", 1), Day = J.I(o, "day", 1), Minute = J.D(o, "minute", gd.DayStart),
                Phase = J.S(o, "phase", Phases.Plan), Speed = J.I(o, "speed", 1), Paused = J.B(o, "paused"), Cash = J.D(o, "cash"),
                Fame = J.D(o, "fame", 30), Researching = J.S(o, "researching"), ResearchLeftS = J.D(o, "research_left"),
                Weather = J.S(o, "weather", "sunny"), Forecast = J.S(o, "forecast", "sunny"), Tutorial = J.B(o, "tutorial"),
                HandStage = J.I(o, "hand"), HandTimer = J.D(o, "hand_timer"), PropaneKits = J.I(o, "kits"),
                OfflinePending = J.D(o, "offline_pending"), LastSeen = J.L(o, "last_seen"), BestDayStars = J.I(o, "best_stars"),
                ThreeStarDays = J.I(o, "three_star_days"), Burgers = J.L(o, "burgers"),
            };
            if (!Finite(s.Minute) || !Finite(s.Cash) || !Finite(s.Fame) || !Finite(s.ResearchLeftS) || !Finite(s.OfflinePending) || !Finite(s.HandTimer)) return Fail("not_finite");
            if (s.Day < 1 || s.Day > 1000000) return Fail("day");
            if (!Phases.Valid(s.Phase)) return Fail("phase");
            if (s.Minute < gd.DayStart - 1 || s.Minute > gd.DayEnd + 1) return Fail("minute");
            if (s.Speed < 1 || s.Speed > 8 || s.HandStage < 0 || s.HandStage > 6 || s.PropaneKits < 0) return Fail("counts");
            if (Array.IndexOf(gd.WeatherIds, s.Weather) < 0 || Array.IndexOf(gd.WeatherIds, s.Forecast) < 0) return Fail("weather");
            s.Districts = J.StrList(o, "districts");
            foreach (var d in s.Districts) if (gd.District(d) == null) return Fail("district");
            if (s.Districts.Count == 0) return Fail("district");
            s.Research = J.StrList(o, "research");
            foreach (var r in s.Research) if (!gd.ResearchById.ContainsKey(r)) return Fail("research");
            if (s.Researching != "" && !gd.ResearchById.ContainsKey(s.Researching)) return Fail("research");
            s.Seen = J.StrList(o, "seen");

            foreach (var x in J.Arr(o, "trucks") ?? new List<object>())
            {
                var t = TruckState.Read(x);
                string bad = CheckTruck(t, gd);
                if (bad != "") return Fail(bad);
                s.Trucks.Add(t);
            }
            if (s.Trucks.Count == 0) return Fail("no_trucks");
            s.Commissary = CommissaryState.Read(J.Obj(o, "commissary"));
            foreach (var q in s.Commissary.Queue) if (Stock.Index(q) < 0) return Fail("prep_item");
            if (!Finite(s.Commissary.PrepLeft)) return Fail("not_finite");
            foreach (var p in J.Arr(o, "pitches") ?? new List<object>())
            {
                var ps = PitchState.Read(p);
                if (ps.Node < 0 || ps.Node >= gd.Nodes.Count || !gd.Nodes[ps.Node].IsPitch) return Fail("pitch_node");
                s.Pitches.Add(ps);
            }
            foreach (var e in J.Arr(o, "events") ?? new List<object>()) s.Events.Add(ActiveEvent.Read(e));
            foreach (var e in J.Arr(o, "planned") ?? new List<object>()) s.Planned.Add(PlannedEvent.Read(e));
            s.Today = DayStats.Read(J.Obj(o, "today"));
            foreach (var h in J.Arr(o, "history") ?? new List<object>()) s.History.Add(DayStats.Read(h));
            if (J.Obj(o, "rewards") is JObj rw) foreach (var kv in rw) s.Rewards[kv.Key] = RewardUse.Read(kv.Value);
            return new DecodeResult { Ok = true, Sim = new Simulation(gd, s) };
        }

        static string CheckTruck(TruckState t, GameData gd)
        {
            if (!gd.Vehicles.TryGetValue(t.Type, out var v)) return "truck_type";
            int n = gd.Nodes.Count;
            if (t.AtNode < 0 || t.AtNode >= n) return "truck_node";
            if (t.Pitch < -1 || t.Pitch >= n || (t.Pitch >= 0 && !gd.Nodes[t.Pitch].IsPitch)) return "truck_pitch";
            if (t.Target < -1 || t.Target >= n) return "truck_target";
            foreach (int p in t.Path) if (p < 0 || p >= n) return "truck_path";
            if (!Finite(t.PathPos) || !Finite(t.PathKm) || !Finite(t.TimeLeft) || !Finite(t.Work) || !Finite(t.Grease)) return "not_finite";
            foreach (var c in t.Crew) if (!Roles.Valid(c)) return "crew_role";
            if (t.Crew.Count > v.Crew) return "crew_count";
            foreach (var m in t.Menu) if (gd.Card(m) == null) return "menu_card";
            int w = t.Extended ? v.WExt : v.W, h = t.Extended ? v.HExt : v.H;
            if (!LayoutOk(t.Layout, w, h)) return "layout";
            if (t.Undo != null && !LayoutOk(t.Undo, w, h)) return "layout";
            foreach (var p in t.Presets) if (!LayoutOk(p, Math.Max(w, v.WExt), Math.Max(h, v.HExt))) return "layout";
            foreach (var c in t.Queue) if (c.Card < 0 || c.Card >= gd.Cards.Count || c.Arch < 0 || c.Arch >= gd.Archetypes.Count || !Finite(c.At)) return "queue";
            return "";
        }

        static bool LayoutOk(List<StationTile> l, int w, int h)
        {
            var seen = new HashSet<int>();
            foreach (var s in l)
            {
                if (!Stations.Valid(s.Type) || s.X < 0 || s.Y < 0 || s.X >= w || s.Y >= h) return false;
                if (!seen.Add(s.Y * 100 + s.X)) return false;
            }
            return true;
        }

        static bool Finite(double d) => !double.IsNaN(d) && !double.IsInfinity(d);
    }
}
