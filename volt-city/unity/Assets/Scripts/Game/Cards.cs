// Volt City — the sheets that open over the live diorama (UI.md section 7): shift report, offline summary, event card
// (with its counterplay), district / fault / plant cards, forecast. Same TabView model as the tabs.
using System;
using System.Collections.Generic;
using VoltCity.Sim;

namespace VoltCity.Game
{
    public static class Cards
    {
        public static string Stars(int n) => new string('★', Math.Max(0, n)) + new string('☆', Math.Max(0, 3 - n));

        // ------------------------------------------------------------------------------------------- shift report

        public static TabView Report(Simulation sim, Loc loc, Extras ext = null)
        {
            var rep = sim.S.Report;
            var t = new TabView { Id = "report", Icon = "report", Title = loc.F("report.title", "n", rep != null ? rep.Shift + 1 : sim.S.ShiftNo + 1) };
            if (rep == null) return t;
            string L = loc.Lang;
            t.Sub = Stars(rep.Stars) + "  " + loc.F("report.uptime", "pct", Fmt.Pm(rep.UptimePm, L));
            t.Add(new Row { Id = "rep.served", Icon = "district", Title = loc.T("report.served"), Sub = loc.F("report.served_sub", "served", Fmt.Int(rep.ServedMwh, L), "demand", Fmt.Int(rep.DemandMwh, L)) });
            t.Add(new Row { Id = "rep.revenue", Icon = "cash", Title = loc.T("report.revenue"), Badge = Fmt.Money(rep.Revenue, L) });
            t.Add(new Row { Id = "rep.fuel", Icon = "plant_steam", Title = loc.T("report.fuel"), Badge = Fmt.Money(-rep.Fuel, L) });
            t.Add(new Row { Id = "rep.upkeep", Icon = "ops", Title = loc.T("report.upkeep"), Badge = Fmt.Money(-rep.Upkeep, L) });
            if (rep.Bonus != 0) t.Add(new Row { Id = "rep.bonus", Icon = "star", Title = loc.T("report.bonus"), Badge = Fmt.Money(rep.Bonus, L) });
            t.Add(new Row { Id = "rep.outage", Icon = "spark", Title = loc.T("report.outage"), Sub = loc.N("count.dark_min", rep.OutageMin) + " · " + loc.N("count.faults", rep.Faults) + " · " + loc.N("count.sheds", rep.Shed) });
            t.Add(new Row { Id = "rep.co2", Icon = "leaf", Title = loc.T("report.co2"), Badge = Stars(rep.Co2Stars) });
            t.Add(new Row { Id = "rep.rating", Icon = "star", Title = loc.T("report.rating"), Badge = Fmt.Rating(sim.S.RatingX100, L) });
            t.Add(new Row { Id = "rep.profit", Icon = "cash", Title = loc.T("report.profit"), Badge = Fmt.Money(rep.Profit, L) });
            // P4: opt-in shift bonus (rewarded) + the storm-recovery offer after a 1-star storm shift
            if (rep.RewardBonus > 0) t.Add(new Row { Id = "rep.reward", Icon = "star", Title = loc.T("report.reward_bonus"), Badge = "+" + Fmt.Money(rep.RewardBonus, L) });
            var bonus = ext?.Rewards?.Row("shift_bonus", loc);
            if (bonus != null) t.Add(bonus);
            if (ext?.Shop != null && ext.Shop.ActiveOffer == "storm_kit") t.Add(ext.Shop.ProductRow("storm_kit", loc, ""));
            t.Primary = new RowAction { Id = "collect", Label = loc.F("report.collect", "cash", Fmt.Money(rep.Profit, L)), Target = "report.collect" };
            return t;
        }

        // ------------------------------------------------------------------------------------------- offline

        public static TabView Offline(OfflineSummary o, Loc loc, Extras ext = null)
        {
            var t = new TabView { Id = "offline", Icon = "moon", Title = loc.T("offline.title") };
            if (o == null) return t;
            t.Sub = loc.F("offline.sub", "time", Fmt.Dur(o.AwayS, loc));
            t.Add(new Row { Id = "off.cash", Icon = "cash", Title = loc.T("offline.earned"), Badge = Fmt.Money(o.Cash, loc.Lang), Sub = loc.T("offline.night_crew") });
            if (o.Capped) t.Add(new Row { Id = "off.cap", Icon = "clock", Title = loc.T("offline.capped"), Sub = loc.F("offline.capped_sub", "time", Fmt.Dur(o.CountedS, loc)) });
            // P4: double it (rewarded, 1 a day) + the comeback offer after >= 3 days away
            if (o.Doubled) t.Rows[0].Badge = Fmt.Money(o.Cash * 2, loc.Lang);
            var x2 = ext?.Rewards?.Row("offline_x2", loc);
            if (x2 != null) t.Add(x2);
            if (ext?.Shop != null && ext.Shop.ActiveOffer == "comeback") t.Add(ext.Shop.ProductRow("comeback", loc, ""));
            t.Primary = new RowAction { Id = "close", Label = loc.T("offline.ok"), Target = "offline.ok" };
            return t;
        }

        // ------------------------------------------------------------------------------------------- event card

        /// <summary>The chaos-event card (event_start): what happens + what to do about it (DESIGN section 5 counterplay).</summary>
        public static TabView Event(string eventId, Simulation sim, Loc loc)
        {
            var t = new TabView { Id = "event:" + eventId, Icon = "event_" + eventId, Title = loc.T("ev." + eventId + ".t"), Sub = loc.T("ev." + eventId + ".b") };
            t.Add(new Row { Id = "ev.tip", Icon = "tip", Title = loc.T("event.counterplay"), Sub = loc.T("ev." + eventId + ".tip") });
            string tab = EventTab(eventId);
            if (tab != "") t.Primary = new RowAction { Id = "open:tab:" + tab, Label = loc.T("event.go." + tab), Target = "event.go" };
            return t;
        }

        /// <summary>The tab that holds the counterplay.</summary>
        public static string EventTab(string eventId)
        {
            switch (eventId)
            {
                case "thunderstorm": case "squirrel": case "balloons": return "crews";
                case "hospital_test": case "new_district_rush": return "city";
                case "": return "";
                default: return "grid";
            }
        }

        /// <summary>Events that get a card when they start (the others only a toast).</summary>
        public static bool EventGetsCard(string id) => id == "thunderstorm" || id == "heatwave" || id == "cold_snap" || id == "match_night"
            || id == "festival" || id == "plant_trip" || id == "fog" || id == "cloud_front" || id == "eclipse" || id == "fuel_spike" || id == "hospital_test";

        // ------------------------------------------------------------------------------------------- forecast

        public static TabView Forecast(Simulation sim, Loc loc)
        {
            var t = new TabView { Id = "forecast", Icon = "cloud", Title = loc.T("forecast.title") };
            var list = sim.Forecast();
            t.Sub = list.Count > 1 ? loc.F("forecast.sub_n", "n", list.Count) : loc.T("forecast.sub");
            for (int k = 0; k < list.Count; k++)
            {
                var plan = list[k];
                var r = t.Add(new Row { Id = "fc:" + k, Icon = "wx_" + plan.Weather, Title = loc.F("forecast.shift", "n", sim.S.ShiftNo + 1 + k, "weather", Names.Weather(loc, plan.Weather)) });
                var names = new List<string>();
                foreach (var e in plan.Events)
                {
                    if (!sim.Gd.Event.TryGetValue(e.Id, out var def)) continue;
                    if (def.Kind == "random" && !def.Forecast) continue; // surprises stay surprises
                    if (e.Id == "halftime_surge") continue;
                    string n = Names.EventTitle(loc, e.Id);
                    if (!names.Contains(n)) names.Add(n);
                }
                r.Sub = names.Count == 0 ? loc.T("forecast.calm") : string.Join(" · ", names);
            }
            return t;
        }

        // ------------------------------------------------------------------------------------------- district / fault / plant

        public static TabView District(string id, Simulation sim, Loc loc)
        {
            var gd = sim.Gd;
            var d = gd.District[id];
            var t = new TabView { Id = "card:district:" + id, Icon = d.Backup ? "hospital" : "district", Title = Names.District(loc, id) };
            t.Sub = loc.T("district." + id + ".flavour");
            var city = Tabs.City(sim, loc);
            var row = city.Rows.Find(r => r.Id == "district:" + id) ?? city.Rows.Find(r => r.Id == "hookup:" + id);
            if (row != null)
            {
                var copy = new Row { Id = "d.state", Icon = row.Icon, Title = row.Section, Sub = row.Sub, Badge = row.Badge };
                copy.Actions.AddRange(row.Actions);
                t.Add(copy);
            }
            if (d.Backup) t.Add(new Row { Id = "d.backup", Icon = "hospital", Title = loc.T("hospital.title"), Sub = loc.T("hospital.body") });
            t.Add(new Row { Id = "d.peak", Icon = "bolt", Title = loc.T("district.peak"), Badge = Fmt.Mw((long)(d.PeakMw * 1000), loc.Lang) });
            var corr = gd.Corridor[d.Corridor];
            int lv = sim.S.Lines.TryGetValue(d.Corridor, out int l) ? l : 0;
            t.Add(new Row { Id = "d.line", Icon = "line", Title = loc.T("district.line"), Sub = loc.F("district.line_sub", "n", Names.Roman(lv + 1), "mw", Fmt.Mw((long)(corr.CapacityMw[Math.Min(2, lv)] * 1000), loc.Lang)) });
            return t;
        }

        public static TabView FaultCard(int faultId, Simulation sim, Loc loc, DispatchFlow dispatch, Extras ext = null)
        {
            var g = sim.Grid;
            var f = g?.FindFault(faultId);
            var t = new TabView { Id = "card:fault:" + faultId, Icon = "spark" };
            if (f == null || !f.Open)
            {
                t.Title = loc.T("fault.fixed.t");
                t.Sub = loc.T("fault.fixed.b");
                return t;
            }
            t.Title = Tabs.FaultTitle(sim, loc, f);
            long homes = Tabs.HomesDark(g, f);
            t.Sub = (homes > 0 ? loc.F("fault.homes", "n", Fmt.Int(homes, loc.Lang)) : loc.T("fault.rerouted")) + " · " + loc.T("cause." + (f.Cause == "" ? "unknown" : f.Cause));
            var ot = ext?.Rewards?.Row("crew_overtime", loc, f.Id);   // P4: instant repair of THIS fault (rewarded)
            if (ot != null) t.Add(ot);
            if (f.Crew >= 0)
            {
                var c = g.Crews[f.Crew];
                t.Add(new Row { Id = "f.crew", Icon = "truck", Title = loc.F("crew.truck", "name", Names.Crew(sim.Gd, c.Id)), Sub = Tabs.CrewLine(sim, loc, c) });
                return t;
            }
            // one-tap path (guide step 4): nearest free truck
            t.Primary = new RowAction { Id = "send:" + f.Id, Label = loc.T("fault.send_nearest"), Enabled = g.IdleCrews() > 0, Target = "card.fault.send" };
            foreach (var c in g.Crews)
            {
                bool free = c.State == CrewTruck.Idle || c.State == CrewTruck.Return;
                var r = t.Add(new Row { Id = "crew:" + c.Id, Icon = "truck", Title = loc.F("crew.truck", "name", Names.Crew(sim.Gd, c.Id)), Sub = Tabs.CrewLine(sim, loc, c), Locked = !free, Target = "card.fault.crew" });
                if (free) r.Act("send:" + f.Id + ":" + c.Id, loc.F("crew.send_here", "s", Fmt.Dur(g.TravelSteps(c.Node, f.Site) / 4.0, loc)), true, "tonal", "card.fault.crew.send");
            }
            if (g.IdleCrews() == 0) t.Add(new Row { Id = "f.busy", Icon = "clock", Title = loc.T("fault.all_busy.t"), Sub = loc.T("fault.all_busy.b") });
            return t;
        }

        public static TabView Plant(string plant, Simulation sim, Loc loc)
        {
            var t = new TabView { Id = "card:plant:" + plant, Icon = "plant_" + plant, Title = Names.Plant(loc, plant) + " " + Names.Roman(Math.Max(1, sim.S.Level(plant))) };
            t.Sub = loc.T("plant." + plant + ".about");
            var grid = Tabs.Grid(sim, loc);
            var row = grid.Rows.Find(r => r.Id == "plant:" + plant);
            if (row != null)
            {
                var copy = new Row { Id = "p.state", Icon = row.Icon, Title = row.Section, Sub = row.Sub, Badge = row.Badge, Progress = row.Progress };
                copy.Actions.AddRange(row.Actions);
                t.Add(copy);
            }
            var g = sim.Grid;
            if (plant == "gas" && g != null && g.POn[Sim.Grid.PGas] == 1)
                t.Primary = new RowAction { Id = "power:gas:off", Label = loc.T("grid.stop_unit"), Kind = "tonal", Target = "card.plant.stop" };
            var def = sim.Gd.Plant[plant];
            string fuel = def.Fuel.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture).Replace(".", Fmt.Dec(loc.Lang));
            t.Add(new Row { Id = "p.fuel", Icon = "cash", Title = loc.T("plant.fuel"), Badge = def.Fuel > 0 ? loc.F("plant.fuel_v", "v", fuel) : loc.T("plant.free_fuel") });
            return t;
        }
    }
}
