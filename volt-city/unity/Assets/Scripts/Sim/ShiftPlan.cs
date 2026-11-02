// Volt City sim core — engine independent (no UnityEngine). C# 9 / .NET Standard 2.1.
using System;
using System.Collections.Generic;

namespace VoltCity.Sim
{
    public sealed class PlanEvent
    {
        public string Id = "", District = "", Plant = "", Corridor = "";
        public int From, To, Pct, TariffPct, WindPct, SolarPct, GasPct, BonusPct;

        public JObj ToJson()
        {
            var o = new JObj { ["id"] = Id, ["from"] = (long)From, ["to"] = (long)To };
            if (Pct != 0) o["pct"] = (long)Pct;
            if (TariffPct != 0) o["tariff_pct"] = (long)TariffPct;
            if (WindPct != 0) o["wind_pct"] = (long)WindPct;
            if (SolarPct != 0) o["solar_pct"] = (long)SolarPct;
            if (GasPct != 0) o["gas_pct"] = (long)GasPct;
            if (BonusPct != 0) o["bonus_pct"] = (long)BonusPct;
            if (District != "") o["district"] = District;
            if (Plant != "") o["plant"] = Plant;
            if (Corridor != "") o["corridor"] = Corridor;
            return o;
        }

        public static PlanEvent FromJson(object o) => new PlanEvent
        {
            Id = J.S(o, "id"), From = J.I(o, "from"), To = J.I(o, "to"), Pct = J.I(o, "pct"), TariffPct = J.I(o, "tariff_pct"), WindPct = J.I(o, "wind_pct"),
            SolarPct = J.I(o, "solar_pct"), GasPct = J.I(o, "gas_pct"), BonusPct = J.I(o, "bonus_pct"), District = J.S(o, "district"), Plant = J.S(o, "plant"),
            Corridor = J.S(o, "corridor")
        };
    }

    public sealed class PlanFault
    {
        public int Step, PartPm, RepairS;
        public string Kind = "line", Corridor = "", District = "", Cause = "";

        public JObj ToJson() => new JObj
        {
            ["step"] = (long)Step, ["kind"] = Kind, ["corridor"] = Corridor, ["district"] = District, ["part_pm"] = (long)PartPm, ["repair_s"] = (long)RepairS, ["cause"] = Cause
        };

        public static PlanFault FromJson(object o) => new PlanFault
        {
            Step = J.I(o, "step"), Kind = J.S(o, "kind", "line"), Corridor = J.S(o, "corridor"), District = J.S(o, "district"), PartPm = J.I(o, "part_pm"),
            RepairS = J.I(o, "repair_s"), Cause = J.S(o, "cause")
        };
    }

    public sealed class PlanTrip
    {
        public int Step, Dur;
        public string Plant = "";
        public JObj ToJson() => new JObj { ["step"] = (long)Step, ["plant"] = Plant, ["dur"] = (long)Dur };
        public static PlanTrip FromJson(object o) => new PlanTrip { Step = J.I(o, "step"), Plant = J.S(o, "plant"), Dur = J.I(o, "dur") };
    }

    /// <summary>Weather kind + chaos events of one shift. Integers / strings only (JSON safe).</summary>
    public sealed class ShiftPlan
    {
        public int Shift, StartMin, HeatLeft, HeatPct;
        public long Seed;
        public string Weather = "sunny";
        public List<PlanEvent> Events = new List<PlanEvent>();
        public List<PlanFault> Faults = new List<PlanFault>();
        public List<PlanTrip> Trips = new List<PlanTrip>();

        public bool Has(string eventId)
        {
            foreach (var e in Events) if (e.Id == eventId) return true;
            return false;
        }

        public JObj ToJson()
        {
            var ev = new List<object>();
            foreach (var e in Events) ev.Add(e.ToJson());
            var fa = new List<object>();
            foreach (var f in Faults) fa.Add(f.ToJson());
            var tr = new List<object>();
            foreach (var t in Trips) tr.Add(t.ToJson());
            return new JObj
            {
                ["shift"] = (long)Shift, ["start_min"] = (long)StartMin, ["weather"] = Weather, ["seed"] = Seed, ["events"] = ev, ["faults"] = fa,
                ["trips"] = tr, ["heat_left"] = (long)HeatLeft, ["heat_pct"] = (long)HeatPct
            };
        }

        public static ShiftPlan FromJson(object o, GameData gd, out string error)
        {
            error = "";
            if (!(o is JObj)) { error = "invalid_plan"; return null; }
            var p = new ShiftPlan
            {
                Shift = J.I(o, "shift"), StartMin = J.I(o, "start_min"), Weather = J.S(o, "weather"), Seed = J.L(o, "seed"), HeatLeft = J.I(o, "heat_left"),
                HeatPct = J.I(o, "heat_pct")
            };
            if (!gd.WeatherById.ContainsKey(p.Weather) || p.StartMin < 0 || p.StartMin >= 1440) { error = "invalid_plan"; return null; }
            var ev = J.Arr(o, "events");
            var fa = J.Arr(o, "faults");
            var tr = J.Arr(o, "trips");
            if (ev == null || fa == null || tr == null || ev.Count > 64 || fa.Count > 64 || tr.Count > 8) { error = "invalid_plan_lists"; return null; }
            foreach (var e in ev)
            {
                var pe = PlanEvent.FromJson(e);
                if (!gd.Event.ContainsKey(pe.Id)) { error = "invalid_plan_event"; return null; }
                p.Events.Add(pe);
            }
            foreach (var f in fa)
            {
                var pf = PlanFault.FromJson(f);
                if (pf.Kind == "line" ? !gd.Corridor.ContainsKey(pf.Corridor) : !gd.District.ContainsKey(pf.District)) { error = "invalid_plan_fault"; return null; }
                p.Faults.Add(pf);
            }
            foreach (var t in tr)
            {
                var pt = PlanTrip.FromJson(t);
                if (gd.PlantIndex(pt.Plant) < 0) { error = "invalid_plan_trip"; return null; }
                p.Trips.Add(pt);
            }
            return p;
        }
    }

    /// <summary>What the planner needs from the career (filled by Simulation.PlanCtx).</summary>
    public sealed class PlanContext
    {
        public int Chapter = 1, ChapterShift, LastStars = 3, HeatLeft, HeatPct, TreeTrim;
        public List<string> Hooked = new List<string>();          // in district order
        public Dictionary<string, int> Plants = new Dictionary<string, int>();
        public List<string> Rush = new List<string>();
        public List<string> Force;                                 // probes / tests: these event ids instead of the random roll
    }

    /// <summary>
    /// Shift planner (DESIGN section 5). The weather chain depends only on the seed (never on the player); event draws
    /// are made in a fixed order whatever the conditions, so the forecast card shows what will happen unless the city
    /// changes first. Caps: at most MaxNegative negative events, no thunderstorm right after a 1-star shift, none in the
    /// tutorial shifts, never two faults on one corridor (also enforced live by Grid). Steps relative to the shift start.
    /// </summary>
    public static class Planner
    {
        const long SaltWeather = 101, SaltEvents = 1009, SaltNoise = 2003, SaltGentle = 3011;

        public static int MinToStep(GameData gd, int startMin, int minuteOfDay)
        {
            int rel = (int)Rng.PosMod(minuteOfDay - startMin, 1440);
            return rel * gd.StepsPerHour / 60;
        }

        public static int StartMinOf(GameData gd, int shiftNo) => shiftNo == 0 ? gd.FirstShiftStartMin : gd.StartMin;

        public static string WeatherKindFor(GameData gd, long seed, int shiftNo, string prevKind)
        {
            var r = Rng.Stream(seed, SaltWeather * 7919 + shiftNo);
            double u = r.Frand();
            if (!string.IsNullOrEmpty(prevKind) && gd.WeatherById.ContainsKey(prevKind) && u * 100.0 < gd.PersistencePct) return prevKind;
            var w = new double[gd.Weather.Count];
            for (int i = 0; i < w.Length; i++) w[i] = gd.Weather[i].Weight;
            int k = r.PickWeighted(w);
            return gd.Weather[k < 0 ? 0 : k].Id;
        }

        public static long ShiftSeed(long seed, int shiftNo) => Rng.Mix(seed, SaltNoise * 31 + shiftNo);

        static bool GentleGuarantee(GameData gd, ShiftPlan plan, PlanContext ctx, long seed, int shiftNo, int startMin, List<string> corridors, HashSet<string> usedCorr)
        {
            foreach (var e in plan.Events) if (e.Id == "squirrel" || e.Id == "balloons" || e.Id == "plant_trip") return false;
            var r = Rng.Stream(seed, SaltGentle * 7919 + shiftNo);
            double pick = r.Frand(), where = r.Frand(), when = r.Frand(), min = r.Frand();
            int at = MinToStep(gd, startMin, (10 + (int)(when * 5.0)) * 60 + (int)(min * 59.0));   // 10:00-14:59
            bool line = corridors.Count > 0 && (pick < 0.5 || ctx.Hooked.Count == 0);
            if (line && gd.Event.TryGetValue("balloons", out var bal))
            {
                string cid = corridors[(int)(where * corridors.Count) % corridors.Count];
                if (usedCorr.Contains(cid)) return false;
                usedCorr.Add(cid);
                plan.Faults.Add(new PlanFault { Step = at, Kind = "line", Corridor = cid, RepairS = bal.RepairS, Cause = bal.Id });
                plan.Events.Add(new PlanEvent { Id = bal.Id, From = at, To = at, Corridor = cid });
                return true;
            }
            if (ctx.Hooked.Count > 0 && gd.Event.TryGetValue("squirrel", out var sq))
            {
                string dist = ctx.Hooked[(int)(where * ctx.Hooked.Count) % ctx.Hooked.Count];
                plan.Faults.Add(new PlanFault { Step = at, Kind = "transformer", District = dist, PartPm = sq.PartPct * 10, RepairS = sq.RepairS, Cause = sq.Id });
                plan.Events.Add(new PlanEvent { Id = sq.Id, From = at, To = at, District = dist });
                return true;
            }
            return false;
        }

        static int LerpI(int[] range, double u)
        {
            if (range == null || range.Length < 2) return range != null && range.Length == 1 ? range[0] : 0;
            int lo = range[0], hi = range[1];
            return lo + Math.Min((int)(u * (hi - lo + 1)), hi - lo);
        }

        public static ShiftPlan Make(GameData gd, long seed, int shiftNo, string wx, PlanContext ctx)
        {
            int startMin = StartMinOf(gd, shiftNo);
            int sph = gd.StepsPerHour;
            int steps = gd.Steps;
            var plan = new ShiftPlan { Shift = shiftNo, StartMin = startMin, Weather = wx, Seed = ShiftSeed(seed, shiftNo) };
            var r = Rng.Stream(seed, SaltEvents * 7919 + shiftNo);
            int chapter = ctx.Chapter;
            bool tutorial = shiftNo < gd.TutorialShifts;
            int negatives = 0;
            var corridors = new List<string>();
            foreach (var d in ctx.Hooked) corridors.Add(gd.District[d].Corridor);
            var usedCorr = new HashSet<string>();
            if (ctx.HeatLeft > 0)
            {
                var hw = gd.Event["heatwave"];
                int pct = ctx.HeatPct > 0 ? ctx.HeatPct : hw.Pct[0];
                plan.Events.Add(new PlanEvent { Id = "heatwave", From = MinToStep(gd, startMin, hw.FromH * 60), To = MinToStep(gd, startMin, hw.ToH * 60), Pct = pct });
                plan.HeatLeft = ctx.HeatLeft - 1;
                plan.HeatPct = pct;
                negatives++;
            }
            foreach (var ev in gd.Events)
            {
                if (ev.Kind != "random") continue;
                string[] units = ev.PerUnit ? new[] { "steam", "gas" } : new[] { "" };
                foreach (var unit in units)
                {
                    // fixed draw order: 6 numbers per (event, unit) whatever happens below
                    double u = r.Frand(), a = r.Frand(), b = r.Frand(), c = r.Frand(), d4 = r.Frand();
                    r.Frand();
                    if (ctx.Force != null)
                    {
                        if (!ctx.Force.Contains(ev.Id)) continue;
                    }
                    else
                    {
                        if (tutorial || chapter < ev.MinChapter) continue;
                        double p = ev.Chance * (chapter == 1 ? gd.Ch1Scale : 1.0);
                        if (u >= p) continue;
                    }
                    if (ev.Negative && negatives >= gd.MaxNegative) continue;
                    if (!string.IsNullOrEmpty(ev.NeedsPlant) && Lv(ctx, ev.NeedsPlant) <= 0) continue;
                    if (unit != "" && Lv(ctx, unit) <= 0) continue;
                    if (ev.NeedsWeather.Count > 0 && !ev.NeedsWeather.Contains(wx)) continue;
                    bool added = true;
                    switch (ev.Id)
                    {
                        case "thunderstorm":
                        {
                            if (gd.NoStormAfterOneStar && ctx.LastStars <= 1) { added = false; break; }
                            if (corridors.Count == 0) { added = false; break; }
                            int h0 = LerpI(ev.StartH, a);
                            int dur = LerpI(ev.DurH, b);
                            int from = MinToStep(gd, startMin, h0 * 60);
                            int to = Math.Min(from + dur * sph, steps - 1);
                            int n = LerpI(ev.Faults, c);
                            int cut = ctx.TreeTrim > 0 && gd.TreeTrimCutPct.Length >= ctx.TreeTrim ? gd.TreeTrimCutPct[ctx.TreeTrim - 1] : 0;
                            var fr = new Rng((long)(d4 * 2147483000.0) + 7);
                            for (int k = 0; k < n; k++)
                            {
                                bool keep = fr.Frand() * 100.0 >= cut;
                                int at = from + fr.RangeI(0, Math.Max(to - from, 1));
                                int ci = fr.RangeI(0, corridors.Count - 1);
                                if (!keep) continue;
                                string cid = corridors[ci];
                                if (usedCorr.Contains(cid)) continue;
                                usedCorr.Add(cid);
                                plan.Faults.Add(new PlanFault { Step = at, Kind = "line", Corridor = cid, RepairS = gd.LineRepairS, Cause = "thunderstorm" });
                            }
                            plan.Events.Add(new PlanEvent { Id = ev.Id, From = from, To = to });
                            break;
                        }
                        case "heatwave":
                        {
                            if (ctx.HeatLeft > 0) { added = false; break; }
                            int pct = LerpI(ev.Pct, a);
                            plan.Events.Add(new PlanEvent { Id = ev.Id, From = MinToStep(gd, startMin, ev.FromH * 60), To = MinToStep(gd, startMin, ev.ToH * 60), Pct = pct });
                            plan.HeatLeft = LerpI(ev.Shifts, b) - 1;
                            plan.HeatPct = pct;
                            break;
                        }
                        case "cold_snap":
                            plan.Events.Add(new PlanEvent { Id = ev.Id, From = MinToStep(gd, startMin, ev.FromH * 60), To = MinToStep(gd, startMin, ev.ToH * 60), Pct = LerpI(ev.Pct, a) });
                            break;
                        case "plant_trip":
                        {
                            int at = MinToStep(gd, startMin, LerpI(ev.StartH, a) * 60 + (int)(b * 59.0));
                            int dur = LerpI(ev.DurS, c) * 4;
                            plan.Trips.Add(new PlanTrip { Step = at, Plant = unit, Dur = dur });
                            plan.Events.Add(new PlanEvent { Id = ev.Id, From = at, To = at + dur, Plant = unit });
                            break;
                        }
                        case "squirrel":
                        {
                            if (ctx.Hooked.Count == 0) { added = false; break; }
                            string dist = ctx.Hooked[(int)(a * ctx.Hooked.Count) % ctx.Hooked.Count];
                            int at = MinToStep(gd, startMin, LerpI(ev.StartH, b) * 60 + (int)(c * 59.0));
                            plan.Faults.Add(new PlanFault { Step = at, Kind = "transformer", District = dist, PartPm = ev.PartPct * 10, RepairS = ev.RepairS, Cause = ev.Id });
                            plan.Events.Add(new PlanEvent { Id = ev.Id, From = at, To = at, District = dist });
                            break;
                        }
                        case "balloons":
                        {
                            if (corridors.Count == 0) { added = false; break; }
                            string cid = corridors[(int)(a * corridors.Count) % corridors.Count];
                            if (usedCorr.Contains(cid)) { added = false; break; }
                            usedCorr.Add(cid);
                            int at = MinToStep(gd, startMin, LerpI(ev.StartH, b) * 60 + (int)(c * 59.0));
                            plan.Faults.Add(new PlanFault { Step = at, Kind = "line", Corridor = cid, RepairS = ev.RepairS, Cause = ev.Id });
                            plan.Events.Add(new PlanEvent { Id = ev.Id, From = at, To = at, Corridor = cid });
                            break;
                        }
                        case "fog":
                        {
                            int from = MinToStep(gd, startMin, LerpI(ev.StartH, a) * 60);
                            plan.Events.Add(new PlanEvent { Id = ev.Id, From = from, To = Math.Min(from + ev.DurH[0] * sph, steps), WindPct = ev.WindPct });
                            break;
                        }
                        case "cloud_front":
                        {
                            int from = MinToStep(gd, startMin, LerpI(ev.StartH, a) * 60);
                            plan.Events.Add(new PlanEvent { Id = ev.Id, From = from, To = Math.Min(from + LerpI(ev.DurH, b) * sph, steps), SolarPct = ev.SolarPct });
                            break;
                        }
                        case "fuel_spike":
                            plan.Events.Add(new PlanEvent { Id = ev.Id, From = 0, To = steps, GasPct = ev.GasPct });
                            break;
                        default:
                            added = false;
                            break;
                    }
                    if (added && ev.Negative) negatives++;
                }
            }
            // chapter 1 after the tutorial shifts: at least one GENTLE incident per shift, in the quiet window 10:00-15:00
            // (owner #183-2b: the diagnostics found 0 incidents in 18 / 18 early shifts and a 60-90 s mid-day lull).
            // Own random stream, so every other draw of an existing plan stays the same. Never a hard one: a squirrel
            // (25 % of a district, 6 s repair) or balloons on a line (4 s repair).
            if (ctx.Force == null && !tutorial && chapter == 1 && negatives < gd.MaxNegative && GentleGuarantee(gd, plan, ctx, seed, shiftNo, startMin, corridors, usedCorr))
                negatives++;   // the gentle incident counts towards the negative cap like any other (caps.max_negative)
            // scheduled (chapter calendar) + triggered
            ChapterDef ch = null;
            foreach (var c in gd.Chapters) if (c.Id == chapter) ch = c;
            int cs = ctx.ChapterShift;
            if (ch != null && !tutorial)
            {
                foreach (var item in ch.Calendar)
                {
                    bool due = item.Every <= 0 ? cs == item.First : (cs >= item.First && (cs - item.First) % item.Every == 0);
                    if (!due || !gd.Event.TryGetValue(item.Event, out var ev)) continue;
                    if (!string.IsNullOrEmpty(ev.District) && !ctx.Hooked.Contains(ev.District)) continue;
                    switch (ev.Id)
                    {
                        case "match_night":
                        {
                            plan.Events.Add(new PlanEvent { Id = ev.Id, From = MinToStep(gd, startMin, ev.FromMin), To = MinToStep(gd, startMin, ev.ToMin), Pct = ev.PctFixed, District = ev.District });
                            var ht = gd.Event["halftime_surge"];
                            int at = MinToStep(gd, startMin, ht.AtMin);
                            plan.Events.Add(new PlanEvent { Id = "halftime_surge", From = at, To = at + ht.DurS[0] * 4, Pct = ht.PctFixed });
                            break;
                        }
                        case "festival":
                            plan.Events.Add(new PlanEvent { Id = ev.Id, From = MinToStep(gd, startMin, ev.FromH * 60), To = MinToStep(gd, startMin, ev.ToH * 60), Pct = ev.PctFixed, TariffPct = ev.TariffPct, District = ev.District });
                            break;
                        case "hospital_test":
                            plan.Events.Add(new PlanEvent { Id = ev.Id, From = MinToStep(gd, startMin, ev.FromH * 60), To = MinToStep(gd, startMin, ev.ToH * 60), District = ev.District, BonusPct = ev.BonusShiftPct });
                            break;
                        case "eclipse":
                            plan.Events.Add(new PlanEvent { Id = ev.Id, From = MinToStep(gd, startMin, ev.FromH * 60), To = MinToStep(gd, startMin, ev.ToH * 60), SolarPct = ev.SolarPct });
                            break;
                    }
                }
            }
            var rush = gd.Event["new_district_rush"];
            foreach (var dist in ctx.Rush)
                plan.Events.Add(new PlanEvent { Id = rush.Id, From = MinToStep(gd, startMin, rush.FromH * 60), To = MinToStep(gd, startMin, rush.ToH * 60), Pct = rush.PctFixed, District = dist });
            // stable sort by step (List.Sort is unstable: sort on (step, insertion index))
            var idx = new List<KeyValuePair<int, PlanFault>>();
            for (int i = 0; i < plan.Faults.Count; i++) idx.Add(new KeyValuePair<int, PlanFault>(i, plan.Faults[i]));
            idx.Sort((x, y) => x.Value.Step != y.Value.Step ? x.Value.Step.CompareTo(y.Value.Step) : x.Key.CompareTo(y.Key));
            plan.Faults.Clear();
            foreach (var kv in idx) plan.Faults.Add(kv.Value);
            return plan;
        }

        static int Lv(PlanContext ctx, string plant) => ctx.Plants.TryGetValue(plant, out int v) ? v : 0;

        public static int NegativeCount(GameData gd, ShiftPlan plan)
        {
            int n = 0;
            foreach (var e in plan.Events) if (gd.Event.TryGetValue(e.Id, out var d) && d.Negative) n++;
            return n;
        }
    }
}
