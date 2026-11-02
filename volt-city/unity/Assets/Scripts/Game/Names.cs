// Volt City — localised names of sim things (districts, plants, investments, events, causes, crews, reasons).
// Keys live in strings.csv: district.<id>, plant.<id>, inv.<kind>, invd.<kind>, ev.<id>.t/.b/.tip, wx.<kind>,
// cause.<id>, why.<reason>. District and plant names are proper names in the fictional city (DESIGN section 10: kept
// as names; P5 decides transliteration per locale in the table, not in code).
using System;
using VoltCity.Sim;

namespace VoltCity.Game
{
    public static class Names
    {
        public static string Roman(int n)
        {
            switch (n)
            {
                case 1: return "I";
                case 2: return "II";
                case 3: return "III";
                case 4: return "IV";
                case 5: return "V";
                default: return n.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        public static string District(Loc loc, string id) => string.IsNullOrEmpty(id) ? "" : loc.T("district." + id);
        public static string Plant(Loc loc, string id) => loc.T("plant." + id);
        public static string Weather(Loc loc, string kind) => loc.T("wx." + kind);
        public static string EventTitle(Loc loc, string id) => loc.T("ev." + id + ".t");

        /// <summary>Crew truck i is driven by a first-name crew (names.json).</summary>
        public static string Crew(GameData gd, int id) => gd.CrewNames.Count == 0 ? "#" + (id + 1) : gd.CrewNames[Math.Abs(id) % gd.CrewNames.Count];

        public static string CorridorDistrict(GameData gd, string corridorId) =>
            gd.Corridor.TryGetValue(corridorId ?? "", out var c) ? c.District : "";

        public static string Investment(Loc loc, GameData gd, InvestmentDef x)
        {
            switch (x.Kind)
            {
                case "plant": return loc.F("inv.plant", "plant", Plant(loc, x.Target), "n", Roman(x.Level));
                case "line": return loc.F("inv.line", "district", District(loc, x.Target), "n", Roman(x.Level));
                case "substation":
                {
                    var t = gd.Tie[x.Target];
                    return loc.F("inv.substation", "a", District(loc, CorridorDistrict(gd, t.A)), "b", District(loc, CorridorDistrict(gd, t.B)));
                }
                case "crew_truck": return loc.F("inv.crew_truck", "n", x.Level);
                case "crew_speed": return loc.F("inv.crew_speed", "n", Roman(x.Level));
                case "tree_trim": return loc.F("inv.tree_trim", "n", Roman(x.Level));
                case "dr": return loc.F("inv.dr", "n", Roman(x.Level));
                case "weather_station": return loc.T("inv.weather_station");
                case "dispatcher": return loc.T("inv.auto." + x.Target);
            }
            return x.Id;
        }

        /// <summary>One grey line: what it does + how much it lifts the contract level.</summary>
        public static string InvestmentDesc(Loc loc, GameData gd, InvestmentDef x)
        {
            string what;
            switch (x.Kind)
            {
                case "plant":
                {
                    var p = gd.Plant[x.Target];
                    long kw = (long)Math.Round(p.CapacityMw[Math.Max(0, Math.Min(2, x.Level - 1))] * 1000);
                    what = loc.F("invd.plant." + x.Target, "mw", Fmt.Mw(kw, loc.Lang));
                    break;
                }
                case "line":
                {
                    var c = gd.Corridor[gd.District[x.Target].Corridor];
                    long from = (long)Math.Round(c.CapacityMw[Math.Max(0, x.Level - 1)] * 1000), to = (long)Math.Round(c.CapacityMw[Math.Min(2, x.Level)] * 1000);
                    what = loc.F("invd.line", "from", Fmt.Mw(from, loc.Lang), "to", Fmt.Mw(to, loc.Lang));
                    break;
                }
                case "dr":
                {
                    int pct = gd.DrPct.Length >= x.Level ? gd.DrPct[x.Level - 1] : 8;
                    what = loc.F("invd.dr", "pct", pct, "s", gd.DrDurS);
                    break;
                }
                case "crew_speed":
                {
                    int pct = gd.CrewSpeedUpgradesPct.Length >= x.Level ? gd.CrewSpeedUpgradesPct[x.Level - 1] - 100 : 25;
                    what = loc.F("invd.crew_speed", "pct", pct);
                    break;
                }
                case "dispatcher": what = loc.T("invd.auto." + x.Target); break;
                default: what = loc.T("invd." + x.Kind); break;
            }
            string contract = loc.F("invd.contract", "pct", Fmt.Pm(x.ValueMilli, loc.Lang).Replace(" %", ""));
            return what + " · " + contract;
        }

        /// <summary>Why a row cannot be bought now (never hidden: the row says why).</summary>
        public static string WhyNot(Loc loc, GameData gd, InvestmentDef x, string reason)
        {
            switch (reason)
            {
                case "needs_district":
                {
                    string d = x.Kind == "plant" ? gd.Plant[x.Target].NeedsDistrict : x.Kind == "line" ? x.Target : "";
                    if (x.Kind == "substation")
                    {
                        var t = gd.Tie[x.Target];
                        return loc.F("why.needs_two", "a", District(loc, CorridorDistrict(gd, t.A)), "b", District(loc, CorridorDistrict(gd, t.B)));
                    }
                    return loc.F("why.needs_district", "district", District(loc, d));
                }
                case "needs_chapter": return loc.F("why.needs_chapter", "n", x.Chapter);
                case "locked_prev": return loc.T("why.locked_prev");
                case "live": return loc.T("why.live");
                case "cash": return loc.T("why.cash");
                case "owned": return loc.T("why.owned");
                case "rating": return loc.T("why.rating");
            }
            return reason;
        }
    }
}
