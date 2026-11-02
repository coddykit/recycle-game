// Ridgeback Rangers sim core — save codec (fleet §14). Encode = canonical JSON of the whole career (a live day included:
// rangers mid-road, the tram mid-loop, open pins, an escape alarm, the report screen, the guide, eggs half warmed). Decode
// never throws and never touches a running game; it accepts every state the sim can produce (negative cash after a bad day,
// an empty queue, the nursery) and refuses only what the sim can never write (unknown species / nodes / roles, indices out
// of range, another game's file). Keeping unreadable files (.unreadable copies, .bak fallback) is CoddyKit.SaveRepository's job.
using System;
using System.Collections.Generic;

namespace RidgebackRangers.Sim
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
            if (J.S(o, "game") != "ridgeback-rangers") return Fail("wrong_game");
            int v = J.I(o, "v", -1);
            if (v < 1 || v > State.Version) return Fail("version");
            var s = new State
            {
                Seed = J.L(o, "seed", 1), RngState = J.L(o, "rng", 1), Day = J.I(o, "day", 1), Minute = J.D(o, "minute", gd.DayStart),
                Phase = J.S(o, "phase", Phases.Plan), Speed = J.I(o, "speed", 1), Paused = J.B(o, "paused"), Cash = J.D(o, "cash"), Cp = J.D(o, "cp"),
                Rating = J.D(o, "rating", 50), NextId = J.I(o, "next_id", 1), GateOpen = J.B(o, "gate"), Incubators = J.I(o, "incubators", gd.StartIncubators),
                Cafe = J.I(o, "cafe"), ChiefOn = J.B(o, "chief_on"), Tutorial = J.B(o, "tutorial"), OfflinePending = J.D(o, "offline_pending"),
                LastSeen = J.L(o, "last_seen"), BestDayStars = J.I(o, "best_stars"), ThreeStarDays = J.I(o, "three_star_days"),
                VisitorsTotal = J.L(o, "visitors_total"), Hatched = J.I(o, "hatched"),
            };
            if (!Finite(s.Minute) || !Finite(s.Cash) || !Finite(s.Cp) || !Finite(s.Rating) || !Finite(s.OfflinePending)) return Fail("not_finite");
            if (s.Day < 1 || s.Day > 1000000) return Fail("day");
            if (!Phases.Valid(s.Phase)) return Fail("phase");
            if (s.Minute < gd.DayStart - 1 || s.Minute > gd.DayEnd + 1) return Fail("minute");
            if (s.Speed < 1 || s.Speed > 8 || s.NextId < 1 || s.Incubators < 0 || s.Incubators > 32 || s.Cafe < 0 || s.Cafe > 2) return Fail("counts");
            s.Zones = J.StrList(o, "zones");
            if (s.Zones.Count == 0) return Fail("zone");
            foreach (var z in s.Zones) if (gd.Zone(z) == null) return Fail("zone");
            s.Upgrades = J.StrList(o, "upgrades");
            s.Scouted = J.StrList(o, "scouted");
            s.Seen = J.StrList(o, "seen");
            s.Found = J.StrList(o, "found");
            foreach (var sp in s.Found) if (gd.Sp(sp) == null) return Fail("species");

            foreach (var x in J.Arr(o, "paddocks") ?? new List<object>())
            {
                var p = PaddockState.Read(x);
                if (p.Node < 0 || p.Node >= gd.Nodes.Count || gd.Nodes[p.Node].Kind != "paddock") return Fail("paddock_node");
                if (s.Paddocks.Exists(q => q.Node == p.Node)) return Fail("paddock_dup");
                s.Paddocks.Add(p);
            }
            foreach (var x in J.Arr(o, "animals") ?? new List<object>())
            {
                var a = AnimalState.Read(x);
                if (gd.Sp(a.Species) == null) return Fail("species");
                if (a.Paddock < -1 || a.Paddock >= s.Paddocks.Count) return Fail("animal_paddock");
                if (!Finite(a.ShowUntil)) return Fail("not_finite");
                s.Animals.Add(a);
            }
            if (J.Obj(o, "stations") is JObj stn)
                foreach (var kv in stn)
                {
                    if (gd.Zone(kv.Key) == null) return Fail("station");
                    s.Stations[kv.Key] = StationState.Read(kv.Value);
                }
            foreach (var x in J.Arr(o, "rangers") ?? new List<object>())
            {
                var r = RangerState.Read(x);
                if (!Roles.Valid(r.Role)) return Fail("role");
                if (r.AtNode < 0 || r.AtNode >= gd.Nodes.Count) return Fail("ranger_node");
                foreach (int n in r.Path) if (n < 0 || n >= gd.Nodes.Count) return Fail("ranger_path");
                if (r.Dig < -1 || r.Dig >= gd.DigSites.Count) return Fail("ranger_dig");
                if (!Finite(r.TimeLeft)) return Fail("not_finite");
                s.Rangers.Add(r);
            }
            foreach (var x in J.Arr(o, "pins") ?? new List<object>())
            {
                var p = PinState.Read(x);
                if (!PinKinds.Valid(p.Kind)) return Fail("pin_kind");
                if (p.Node < 0 || p.Node >= gd.Nodes.Count) return Fail("pin_node");
                if (p.Paddock < -1 || p.Paddock >= s.Paddocks.Count || p.Animal < -1 || p.Animal >= s.Animals.Count) return Fail("pin_target");
                s.Pins.Add(p);
            }
            s.Tram = TramState.Read(J.Obj(o, "tram"));
            if (s.Tram.Cars > 16) return Fail("counts");
            foreach (var x in J.Arr(o, "queue") ?? new List<object>()) s.Queue.Add(VisitorGroup.Read(x));
            foreach (var x in J.Arr(o, "eggs") ?? new List<object>())
            {
                var e = EggState.Read(x);
                if (gd.Sp(e.Species) == null) return Fail("species");
                s.Eggs.Add(e);
            }
            s.EggsWaiting = J.StrList(o, "eggs_waiting");
            foreach (var sp in s.EggsWaiting) if (gd.Sp(sp) == null) return Fail("species");
            if (J.Obj(o, "shards") is JObj sh) foreach (var kv in sh) s.Shards[kv.Key] = Math.Max(0, Math.Min(100, J.D(kv.Value)));
            if (J.Obj(o, "dig_eggs") is JObj de) foreach (var kv in de) s.DigEggs[kv.Key] = Math.Max(0, J.I(kv.Value));
            foreach (var x in J.Arr(o, "events") ?? new List<object>()) s.Events.Add(ActiveEvent.Read(x));
            foreach (var x in J.Arr(o, "planned") ?? new List<object>()) s.Planned.Add(PlannedEvent.Read(x));
            s.Today = DayStats.Read(J.Obj(o, "today"));
            foreach (var h in J.Arr(o, "history") ?? new List<object>()) s.History.Add(DayStats.Read(h));
            if (J.Obj(o, "rewards") is JObj rw) foreach (var kv in rw) s.Rewards[kv.Key] = RewardUse.Read(kv.Value);
            return new DecodeResult { Ok = true, Sim = new Simulation(gd, s) };
        }

        static bool Finite(double d) => !double.IsNaN(d) && !double.IsInfinity(d);
    }
}
