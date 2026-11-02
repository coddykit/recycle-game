// Volt City — the 5 tabs (UI.md section 3) as view models: Grid · Crews · City · Build · Shop. Pure functions of the
// sim state + language (+ the dispatch selection); the UI calls them at <= 4 Hz and reconciles rows by Row.Id.
using System;
using System.Collections.Generic;
using VoltCity.Sim;

namespace VoltCity.Game
{
    public static class Tabs
    {
        public static readonly string[] Ids = { "grid", "crews", "city", "build", "shop" };
        public static readonly string[] Icons = { "bolt", "truck", "city", "build", "bag" };

        public static TabView Build(string id, Simulation sim, Loc loc, DispatchFlow dispatch = null, Extras ext = null)
        {
            switch (id)
            {
                case "grid": return Grid(sim, loc, ext);
                case "crews": return Crews(sim, loc, dispatch);
                case "city": return City(sim, loc);
                case "build": return BuildTab(sim, loc);
                case "shop": return ext?.Shop != null ? ext.Shop.ShopTab(loc) : Shop(sim, loc);
            }
            return new TabView { Id = id, Title = id };
        }

        // ------------------------------------------------------------------------------------------- Grid

        static string PlantTitle(Simulation sim, Loc loc, string plant) =>
            Names.Plant(loc, plant) + " " + Names.Roman(Math.Max(1, sim.S.Level(plant)));

        /// <summary>Seconds until the plant's queued setpoint change starts (the "takes effect in 6 s" label), -1 = none.</summary>
        public static double PendingInS(Grid g, int p)
        {
            int best = -1;
            foreach (var pc in g.Pending) if (pc[1] == p) best = Math.Max(best, pc[0] - g.Step);
            return best < 0 ? -1 : best / 4.0;
        }

        static long TargetKw(Grid g, int p, int steps)
        {
            if (g.POn[p] == 0) return 0;
            return Math.Max(g.PMin[p], (long)steps * g.PCap[p] / 10);
        }

        public static TabView Grid(Simulation sim, Loc loc, Extras ext = null)
        {
            var gd = sim.Gd;
            var s = sim.S;
            var g = sim.Grid;
            var t = new TabView { Id = "grid", Icon = "bolt", Title = loc.T("tab.grid") };
            if (g != null)
            {
                long gap = (long)g.LastSupply - g.LastDemand;
                t.Sub = loc.F("grid.sub", "supply", Fmt.Mw(g.LastSupply, loc.Lang), "demand", Fmt.Mw(g.LastDemand, loc.Lang),
                    "pct", (gap >= 0 ? "+" : "") + Fmt.Pm(g.NeedlePm, loc.Lang));
            }
            else
            {
                var f = sim.Forecast();
                t.Sub = f.Count > 0 ? loc.F("grid.sub_idle", "weather", Names.Weather(loc, f[0].Weather)) : "";
                if (s.Phase == "idle") t.Primary = new RowAction { Id = "shift:start", Label = loc.F("grid.start", "n", s.ShiftNo + 1), Target = "grid.start" };
                else if (s.Phase == "report") t.Primary = new RowAction { Id = "open:report", Label = loc.T("grid.see_report"), Target = "grid.report" };
            }
            for (int p = 0; p < GameData.Plants.Length; p++)
            {
                string id = GameData.Plants[p];
                int lv = s.Level(id);
                if (lv == 0) continue;
                var r = new Row { Id = "plant:" + id, Icon = "plant_" + id, Title = PlantTitle(sim, loc, id), Open = "card:plant:" + id, Target = "grid." + id };
                if (g == null)
                {
                    r.Section = loc.T("grid.sec.ready");
                    if (p <= Sim.Grid.PHydro)
                    {
                        int steps = s.Carry.Steps.TryGetValue(id, out int st) ? st : 0;
                        long cap = (long)Math.Round(gd.Plant[id].CapacityMw[lv - 1] * 1000);
                        r.Sub = loc.F("grid.plant.idle", "mw", Fmt.Mw(steps * cap / 10, loc.Lang), "cap", Fmt.Mw(cap, loc.Lang));
                    }
                    else r.Sub = loc.T("grid.plant.idle_" + id);
                    t.Add(r);
                    continue;
                }
                bool tripped = g.PTrip[p] > g.Step;
                bool running = g.POut[p] > 0 || (g.POn[p] == 1 && p <= Sim.Grid.PHydro);
                r.Section = loc.T(running ? "grid.sec.running" : "grid.sec.standby");
                r.Progress = g.PCap[p] > 0 ? Math.Max(0f, Math.Min(1f, (float)Math.Abs(g.POut[p]) / g.PCap[p])) : 0f;
                string outCap = loc.F("grid.out_cap", "out", Fmt.Mw(Math.Abs(g.POut[p]), loc.Lang).Replace(" MW", ""), "cap", Fmt.Mw(g.PCap[p], loc.Lang));
                if (tripped) r.Badge = loc.F("grid.tripped", "s", Fmt.Dur((g.PTrip[p] - g.Step) / 4.0, loc));
                switch (p)
                {
                    case Sim.Grid.PSteam:
                    case Sim.Grid.PGas:
                    case Sim.Grid.PHydro:
                    {
                        int after = g.SetpointAfterPending(p);
                        double inS = PendingInS(g, p);
                        string ramp;
                        // "in 0 s" read oddly (device play 10-29): the last half second already says "Ramping to"
                        if (inS >= 0.5) ramp = loc.F("grid.ramp_in", "mw", Fmt.Mw(TargetKw(g, p, after), loc.Lang), "s", Fmt.Dur(inS, loc));
                        else if (inS >= 0) ramp = loc.F("grid.ramping", "mw", Fmt.Mw(TargetKw(g, p, after), loc.Lang));
                        else if (g.POn[p] == 1 && Math.Abs(g.POut[p] - TargetKw(g, p, g.PSteps[p])) > g.PCap[p] / 50) ramp = loc.F("grid.ramping", "mw", Fmt.Mw(TargetKw(g, p, g.PSteps[p]), loc.Lang));
                        else ramp = loc.F("grid.setpoint", "n", g.PSteps[p]);
                        r.Sub = outCap + " · " + ramp;
                        if (p == Sim.Grid.PHydro && g.ResCapWh > 0) r.Sub += " · " + loc.F("grid.reservoir", "pct", g.ResWh * 100 / g.ResCapWh);
                        if (p == Sim.Grid.PGas && g.POn[p] == 0)
                        {
                            r.Act("power:gas:on", loc.T("grid.start_unit"), !tripped, "primary", "grid.gas.start");
                        }
                        else
                        {
                            r.Act("ramp:" + id + ":-1", "−", !tripped && after > 0, "tonal", "grid." + id + ".minus");
                            r.Act("ramp:" + id + ":+1", "+", !tripped && after < 10, "primary", "grid." + id + ".plus");
                        }
                        break;
                    }
                    case Sim.Grid.PWind:
                    case Sim.Grid.PSolar:
                        r.Sub = outCap + " · " + loc.T(p == Sim.Grid.PWind ? "grid.wind_note" : "grid.solar_note");
                        break;
                    case Sim.Grid.PBattery:
                    {
                        long soc = g.BatCapWh > 0 ? g.BatSoc * 100 / g.BatCapWh : 0;
                        string flow = g.BatOut > 0 ? loc.F("grid.bat_out", "mw", Fmt.Mw(g.BatOut, loc.Lang)) : g.BatOut < 0 ? loc.F("grid.bat_in", "mw", Fmt.Mw(-g.BatOut, loc.Lang)) : loc.T("grid.bat_idle");
                        r.Sub = loc.F("grid.bat", "pct", soc) + " · " + flow;
                        r.Progress = g.BatCapWh > 0 ? (float)g.BatSoc / g.BatCapWh : 0f;
                        for (int m = 0; m < Sim.Grid.Modes.Length; m++)
                            r.Act("bat:" + Sim.Grid.Modes[m], loc.T("bat." + Sim.Grid.Modes[m]), true, "segment", "grid.battery." + Sim.Grid.Modes[m], g.BatMode == m);
                        break;
                    }
                }
                t.Add(r);
            }
            if (s.Dr > 0 && g != null)
            {
                var r = new Row { Id = "dr", Icon = "meter", Title = loc.T("grid.dr"), Section = loc.T("grid.sec.tools"), Target = "grid.dr" };
                if (g.Step < g.DrUntil) r.Sub = loc.F("grid.dr_active", "s", Fmt.Dur((g.DrUntil - g.Step) / 4.0, loc));
                else if (g.Step < g.DrCdUntil) r.Sub = loc.F("grid.dr_cooldown", "s", Fmt.Dur((g.DrCdUntil - g.Step) / 4.0, loc));
                else r.Sub = loc.F("grid.dr_ready", "pct", gd.DrPct[Math.Max(1, s.Dr) - 1]);
                r.Act("dr", loc.F("grid.dr_call", "cost", Fmt.Money(gd.DrCost, loc.Lang)), g.Step >= g.DrCdUntil && s.Cash >= gd.DrCost, "primary", "grid.dr.call");
                t.Add(r);
            }
            // P4: the "last chance" opt-in import during a live deficit (rewarded; the sim decides when it may be offered)
            var imp = ext?.Rewards?.Row("emergency_import", loc);
            if (imp != null && g != null)
            {
                imp.Section = loc.T("grid.sec.tools");
                t.Add(imp);
            }
            return t;
        }

        // ------------------------------------------------------------------------------------------- Crews

        /// <summary>Homes dark behind a fault right now (0 when rerouted / not connected).</summary>
        public static long HomesDark(Grid g, Fault f)
        {
            if (g == null) return 0;
            if (f.District >= 0) return Fmt.Homes((long)g.Dem[f.District][Math.Min(g.Step, g.Steps - 1)] * f.Part / 1000);
            long kw = 0;
            for (int i = 0; i < g.DIds.Length; i++)
                if (g.DHooked[i] && g.DCorr[i] == f.Corridor && !g.IsLit(i)) kw += g.Dem[i][Math.Min(g.Step, g.Steps - 1)];
            return Fmt.Homes(kw);
        }

        public static string FaultTitle(Simulation sim, Loc loc, Fault f)
        {
            var g = sim.Grid;
            string d = f.District >= 0 ? g.DIds[f.District] : Names.CorridorDistrict(sim.Gd, g.CIds[f.Corridor]);
            return loc.F(f.Kind == "line" ? "fault.line" : "fault.transformer", "district", Names.District(loc, d));
        }

        public static string CrewLine(Simulation sim, Loc loc, CrewTruck c)
        {
            var g = sim.Grid;
            switch (c.State)
            {
                case CrewTruck.Travel:
                {
                    var f = g.FindFault(c.Job);
                    return loc.F("crew.driving", "place", f != null ? FaultTitle(sim, loc, f) : "", "s", Fmt.Dur((c.Until - g.Step) / 4.0, loc));
                }
                case CrewTruck.Repair: return loc.F("crew.repairing", "s", Fmt.Dur((c.Until - g.Step) / 4.0, loc));
                case CrewTruck.Return: return loc.T("crew.returning");
                default: return c.Node == sim.Gd.Depot ? loc.T("crew.depot") : loc.T("crew.on_site");
            }
        }

        public static TabView Crews(Simulation sim, Loc loc, DispatchFlow dispatch)
        {
            var gd = sim.Gd;
            var g = sim.Grid;
            var t = new TabView { Id = "crews", Icon = "truck", Title = loc.T("tab.crews") };
            if (g == null)
            {
                t.Sub = loc.N("count.trucks_depot", sim.S.Crews);
                for (int i = 0; i < sim.S.Crews; i++)
                    t.Add(new Row { Id = "crew:" + i, Section = loc.T("crews.sec.trucks"), Icon = "truck", Title = loc.F("crew.truck", "name", Names.Crew(gd, i)), Sub = loc.T("crew.depot") });
                return t;
            }
            var open = g.OpenFaults();
            int free = g.IdleCrews();
            t.Sub = loc.N("count.faults", open.Count) + " · " + loc.N("count.trucks_ready", free);
            var order = new List<Fault>(open);
            var homes = new Dictionary<int, long>();
            foreach (var f in order) homes[f.Id] = HomesDark(g, f);
            order.Sort((a, b) => homes[b.Id] != homes[a.Id] ? homes[b.Id].CompareTo(homes[a.Id]) : a.Id.CompareTo(b.Id));
            int selected = dispatch != null ? dispatch.Fault : -1;
            foreach (var f in order)
            {
                var r = new Row
                {
                    Id = "fault:" + f.Id, Section = loc.T("crews.sec.faults"), Icon = "spark", Title = FaultTitle(sim, loc, f),
                    Open = "card:fault:" + f.Id, Target = "crews.fault", Selected = f.Id == selected,
                };
                string cause = loc.T("cause." + (string.IsNullOrEmpty(f.Cause) ? "unknown" : f.Cause));
                r.Sub = (homes[f.Id] > 0 ? loc.F("fault.homes", "n", Fmt.Int(homes[f.Id], loc.Lang)) : loc.T("fault.rerouted")) + " · " + cause;
                if (f.Crew >= 0)
                {
                    var c = g.Crews[f.Crew];
                    r.Badge = c.State == CrewTruck.Repair ? loc.F("fault.repairing", "s", Fmt.Dur((c.Until - g.Step) / 4.0, loc))
                        : loc.F("fault.eta", "name", Names.Crew(gd, c.Id), "s", Fmt.Dur((c.Until - g.Step) / 4.0, loc));
                }
                else r.Act("select:" + f.Id, loc.T("fault.send"), free > 0, "primary", "crews.fault.send");
                t.Add(r);
            }
            if (open.Count == 0)
                t.Add(new Row { Id = "faults.none", Section = loc.T("crews.sec.faults"), Icon = "check", Title = loc.T("crews.none.t"), Sub = loc.T("crews.none.b") });
            foreach (var c in g.Crews)
            {
                var r = new Row { Id = "crew:" + c.Id, Section = loc.T("crews.sec.trucks"), Icon = "truck", Title = loc.F("crew.truck", "name", Names.Crew(gd, c.Id)), Sub = CrewLine(sim, loc, c), Target = "crews.crew" };
                bool canGo = c.State == CrewTruck.Idle || c.State == CrewTruck.Return;
                if (selected >= 0 && canGo)
                {
                    int eta = g.TravelSteps(c.Node, g.FindFault(selected)?.Site ?? c.Node);
                    r.Act("send:" + selected + ":" + c.Id, loc.F("crew.send_here", "s", Fmt.Dur(eta / 4.0, loc)), true, "primary", "crews.crew.send");
                }
                t.Add(r);
            }
            return t;
        }

        // ------------------------------------------------------------------------------------------- City

        /// <summary>Hooked districts in shedding order (first to go dark first; Hospital Hill always last).</summary>
        public static List<string> ShedOrder(Simulation sim)
        {
            var o = new List<DistrictDef>();
            foreach (var d in sim.Gd.Districts) if (sim.S.IsHooked(d.Id)) o.Add(d);
            o.Sort((a, b) => a.Priority != b.Priority ? a.Priority.CompareTo(b.Priority) : a.Order.CompareTo(b.Order));
            return o.ConvertAll(d => d.Id);
        }

        public static TabView City(Simulation sim, Loc loc)
        {
            var gd = sim.Gd;
            var s = sim.S;
            var g = sim.Grid;
            var t = new TabView { Id = "city", Icon = "city", Title = loc.T("tab.city") };
            int hooked = 0, lit = 0;
            var order = ShedOrder(sim);
            foreach (var d in gd.Districts)
            {
                if (!s.IsHooked(d.Id)) continue;
                hooked++;
                int i = g != null ? Array.IndexOf(g.DIds, d.Id) : -1;
                if (g == null || (i >= 0 && g.IsLit(i))) lit++;
            }
            t.Sub = loc.F("city.sub", "lit", lit, "n", gd.Districts.Count, "rating", Fmt.Rating(s.RatingX100, loc.Lang));
            string next = Goals.NextDistrict(sim);
            if (next != "")
            {
                var d = gd.District[next];
                string why = Economy.HookupWhyNot(sim, next);
                var r = t.Add(new Row
                {
                    Id = "hookup:" + next, Section = loc.T("city.sec.goal"), Icon = "flag", Title = loc.F("city.hookup", "district", Names.District(loc, next)),
                    Open = "card:district:" + next, Target = "city." + next,
                });
                // ready: the active price pill. Not yet: a lock chip with the reason; the line under the name keeps the
                // price (and the rating when that is the reason) — Patron #173: a grey "$2,000" read as disabled / broken
                string cost = loc.F("city.hookup_cash", "cost", Fmt.Money(d.Hookup, loc.Lang));
                r.Sub = why == "" ? loc.F("city.hookup_ready", "mw", Fmt.Mw((long)(d.PeakMw * 1000), loc.Lang))
                    : why == "rating" ? loc.F("city.hookup_rating", "need", Fmt.Rating((int)Math.Round(d.RatingReq * 100), loc.Lang), "now", Fmt.Rating(s.RatingX100, loc.Lang)) + " · " + cost
                    : cost;
                if (why == "") r.Act("hookup:" + next, Fmt.Money(d.Hookup, loc.Lang), true, "primary", "city.hookup.pill");
                else r.Act("card:district:" + next, why == "live" ? loc.T("why.live") : Names.WhyNot(loc, gd, null, why), true, "status", "city.hookup.pill");
            }
            foreach (var d in gd.Districts)
            {
                var r = new Row { Id = "district:" + d.Id, Icon = d.Backup ? "hospital" : "district", Title = Names.District(loc, d.Id), Open = "card:district:" + d.Id, Target = "city." + d.Id };
                if (!s.IsHooked(d.Id))
                {
                    if (d.Id == next) continue; // already the goal card
                    r.Section = loc.T("city.sec.off");
                    r.Locked = true;
                    r.Sub = loc.F("city.needs", "rating", Fmt.Rating((int)Math.Round(d.RatingReq * 100), loc.Lang), "cost", Fmt.Money(d.Hookup, loc.Lang));
                    t.Add(r);
                    continue;
                }
                int k = order.IndexOf(d.Id) + 1;
                int i = g != null ? Array.IndexOf(g.DIds, d.Id) : -1;
                bool isLit = g == null || (i >= 0 && g.IsLit(i));
                r.Section = loc.T(isLit ? "city.sec.lit" : "city.sec.dark");
                long kw = g != null && i >= 0 ? g.Dem[i][Math.Min(g.Step, g.Steps - 1)] : (long)(d.PeakMw * 1000);
                string order1 = d.Backup ? loc.T("city.hospital_order") : loc.F("city.order", "k", k, "n", order.Count);
                r.Sub = (g != null ? Fmt.Mw(kw, loc.Lang) : loc.F("city.peak", "mw", Fmt.Mw(kw, loc.Lang))) + " · " + order1;
                if (g != null && i >= 0)
                {
                    int st = g.DState[i];
                    if (st == Sim.Grid.Backup) r.Badge = loc.F("city.backup", "s", Fmt.Dur(Math.Max(0, g.DBackupUntil[i] - g.Step) / 4.0, loc));
                    else if (st == Sim.Grid.AutoShed) r.Badge = loc.T("city.auto_shed");
                    else if (st == Sim.Grid.ManualShed) r.Badge = loc.T("city.hand_shed");
                    else if (g.Route(i) < 0) r.Badge = loc.T("city.no_line");
                    else if (g.DPart[i] > 0) r.Badge = loc.F("city.part_dark", "pct", g.DPart[i] / 10);
                    if (st == Sim.Grid.AutoShed || st == Sim.Grid.ManualShed) r.Act("restore:" + d.Id, loc.T("city.restore"), true, "primary", "city." + d.Id + ".restore");
                    else if (!d.Backup && st == Sim.Grid.Lit) r.Act("shed:" + d.Id, loc.T("city.shed"), true, "danger", "city." + d.Id + ".shed");
                }
                t.Add(r);
            }
            return t;
        }

        // ------------------------------------------------------------------------------------------- Build

        /// <summary>The next unowned investment of every chain (plant type, line, substation, operation), data order.</summary>
        public static List<InvestmentDef> NextOfChains(Simulation sim)
        {
            var seen = new HashSet<string>();
            var o = new List<InvestmentDef>();
            var list = new List<InvestmentDef>(sim.Gd.Investments);
            list.Sort((a, b) => a.Level != b.Level ? a.Level.CompareTo(b.Level) : a.Order.CompareTo(b.Order));
            foreach (var x in list)
            {
                if (sim.S.Owned.ContainsKey(x.Id)) continue;
                string chain = x.Kind + ":" + (x.Kind == "crew_truck" || x.Kind == "crew_speed" || x.Kind == "tree_trim" || x.Kind == "dr" ? "" : x.Target);
                if (!seen.Add(chain)) continue;
                o.Add(x);
            }
            o.Sort((a, b) => a.Order.CompareTo(b.Order));
            return o;
        }

        public static string BuildSection(InvestmentDef x)
        {
            switch (x.Kind)
            {
                case "plant": return "build.sec.plants";
                case "line":
                case "substation": return "build.sec.lines";
                default: return "build.sec.ops";
            }
        }

        public static TabView BuildTab(Simulation sim, Loc loc)
        {
            var gd = sim.Gd;
            var t = new TabView { Id = "build", Icon = "build", Title = loc.T("tab.build") };
            var cheapest = Economy.BuyableNow(sim).Find(b => b.Kind == "investment");
            if (cheapest != null) t.Sub = loc.F("build.sub", "name", Names.Investment(loc, gd, gd.Investment[cheapest.Id]), "cost", Fmt.Money(cheapest.Cost, loc.Lang));
            else t.Sub = loc.T(sim.S.Phase == "live" ? "build.sub_live" : "build.sub_none");
            var rows = new List<Row>();
            foreach (var key in new[] { "build.sec.plants", "build.sec.lines", "build.sec.ops" })
            {
                foreach (var x in NextOfChains(sim))
                {
                    if (BuildSection(x) != key) continue;
                    string why = Economy.WhyNot(sim, x.Id);
                    var r = new Row
                    {
                        Id = "inv:" + x.Id, Section = loc.T(key), Icon = x.Kind == "plant" ? "plant_" + x.Target : x.Kind == "line" || x.Kind == "substation" ? "line" : "ops",
                        Title = Names.Investment(loc, gd, x), Target = "build." + x.Id,
                    };
                    bool hard = why != "" && why != "cash" && why != "live";
                    r.Locked = hard;
                    r.Sub = hard ? Names.WhyNot(loc, gd, x, why) : Names.InvestmentDesc(loc, gd, x);
                    if (why == "live") r.Badge = loc.T("build.after_shift");
                    if (why == "needs_chapter" && x.Kind == "dispatcher")
                        r.Act("shop:dispatcher", loc.T("build.in_shop"), true, "tonal", "build." + x.Id + ".shop");
                    else
                        r.Act("buy:" + x.Id, Fmt.Money(x.Cost, loc.Lang), why == "", "primary", "build." + x.Id + ".buy");
                    rows.Add(r);
                }
            }
            t.Rows.AddRange(rows);
            return t;
        }

        // ------------------------------------------------------------------------------------------- Shop (P3 fallback; P4 = Commerce.ShopTab)

        public static TabView Shop(Simulation sim, Loc loc)
        {
            var t = new TabView { Id = "shop", Icon = "bag", Title = loc.T("tab.shop"), Sub = loc.T("shop.sub") };
            t.Add(new Row { Id = "shop.soon", Icon = "bag", Title = loc.T("shop.soon.t"), Sub = loc.T("shop.soon.b") });
            t.Add(new Row { Id = "shop.restore", Icon = "restore", Title = loc.T("shop.restore.t"), Sub = loc.T("shop.restore.b") })
                .Act("restore", loc.T("shop.restore"), true, "tonal", "shop.restore");
            return t;
        }
    }
}
