// Volt City sim core — engine independent (no UnityEngine). C# 9 / .NET Standard 2.1.
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace VoltCity.Sim
{
    public sealed class Buyable
    {
        public string Kind, Id;       // "investment" | "hookup"
        public long Cost;
    }

    public sealed class RewardResult
    {
        public bool Ok;
        public long Amount;
        public string Reason = "";
    }

    public sealed class FundsResult
    {
        public bool Ok, Duplicate;
        public long Granted;
        public string Reason = "";
    }

    /// <summary>
    /// Money side of the career: investments + prerequisites, district hookups, the contract level (price level), shift
    /// money, offline "night crew" pay, the capped rewards API (CLAUDE section 4), idempotent store funds. Callers pass
    /// unix seconds; nothing here reads a clock of its own.
    /// </summary>
    public static class Economy
    {
        /// <summary>Contract level in milli (1000 = x1.0): 1 + the value of every investment owned. Multiplies tariff, fuel and upkeep.</summary>
        public static long PriceMilli(Simulation sim)
        {
            long p = 1000;
            foreach (var id in sim.S.Owned.Keys)
                if (sim.Gd.Investment.TryGetValue(id, out var x)) p += x.ValueMilli;
            return p;
        }

        /// <summary>"" when buyable now, else the reason (owned, live, locked_prev, needs_district, needs_chapter, cash, unknown).</summary>
        public static string WhyNot(Simulation sim, string id)
        {
            if (!sim.Gd.Investment.TryGetValue(id ?? "", out var inv)) return "unknown";
            var s = sim.S;
            if (s.Owned.ContainsKey(id)) return "owned";
            if (s.Phase == "live") return "live";
            int lv = inv.Level;
            switch (inv.Kind)
            {
                case "plant":
                {
                    if (s.Level(inv.Target) != lv - 1) return "locked_prev";
                    var p = sim.Gd.Plant[inv.Target];
                    if (!string.IsNullOrEmpty(p.NeedsDistrict) && !s.IsHooked(p.NeedsDistrict)) return "needs_district";
                    break;
                }
                case "line":
                {
                    if (!s.IsHooked(inv.Target)) return "needs_district";
                    string corr = sim.Gd.District[inv.Target].Corridor;
                    if ((s.Lines.TryGetValue(corr, out int cl) ? cl : 0) != lv - 1) return "locked_prev";
                    break;
                }
                case "substation":
                {
                    var tie = sim.Gd.Tie[inv.Target];
                    foreach (var c in new[] { tie.A, tie.B })
                    {
                        bool ok = false;
                        foreach (var d in s.Hooked.Keys) if (sim.Gd.District[d].Corridor == c) ok = true;
                        if (!ok) return "needs_district";
                    }
                    break;
                }
                case "crew_truck": if (s.Crews != lv - 1) return "locked_prev"; break;
                case "crew_speed": if (s.CrewSpeed != lv - 1) return "locked_prev"; break;
                case "tree_trim": if (s.TreeTrim != lv - 1) return "locked_prev"; break;
                case "dr": if (s.Dr != lv - 1) return "locked_prev"; break;
                case "dispatcher":
                    if (s.Chapter < inv.Chapter && !sim.Perk("dispatcher")) return "needs_chapter";
                    break;
            }
            if (s.Cash < inv.Cost) return "cash";
            return "";
        }

        public static bool Buy(Simulation sim, string id)
        {
            if (WhyNot(sim, id) != "") return false;
            var inv = sim.Gd.Investment[id];
            var s = sim.S;
            s.Cash -= inv.Cost;
            s.Owned[id] = s.ShiftNo;
            switch (inv.Kind)
            {
                case "plant":
                    s.Plants[inv.Target] = inv.Level;
                    if (inv.Target == "hydro" && inv.Level == 1) s.Carry.ResWh = (long)(sim.Gd.Plant["hydro"].ReservoirMwh[0] * 1e6) / 2;
                    break;
                case "line": s.Lines[sim.Gd.District[inv.Target].Corridor] = inv.Level; break;
                case "substation": s.Ties.Add(inv.Target); break;
                case "crew_truck": s.Crews = inv.Level; break;
                case "crew_speed": s.CrewSpeed = inv.Level; break;
                case "tree_trim": s.TreeTrim = inv.Level; break;
                case "dr": s.Dr = inv.Level; break;
                case "weather_station": s.WeatherStation = inv.Level; break;
                case "dispatcher": s.Auto.Add(inv.Target); break;
            }
            s.Stats.Spent += inv.Cost;
            s.Stats.Purchases++;
            sim.Emit(new SimEvent("bought").With("id", id).With("cost", inv.Cost));
            return true;
        }

        public static string HookupWhyNot(Simulation sim, string id)
        {
            if (!sim.Gd.District.TryGetValue(id ?? "", out var d)) return "unknown";
            var s = sim.S;
            if (s.IsHooked(id)) return "owned";
            if (s.Phase == "live") return "live";
            foreach (var o in sim.Gd.Districts)
            {
                if (o.Id == id) break;
                if (!s.IsHooked(o.Id)) return "locked_prev";
            }
            if (s.RatingX100 < (int)Math.Round(d.RatingReq * 100.0)) return "rating";
            if (s.Cash < d.Hookup) return "cash";
            return "";
        }

        public static bool Hookup(Simulation sim, string id)
        {
            if (HookupWhyNot(sim, id) != "") return false;
            var d = sim.Gd.District[id];
            var s = sim.S;
            s.Cash -= d.Hookup;
            s.Hooked[id] = 0;
            s.Rush.Add(id);
            s.Stats.Spent += d.Hookup;
            sim.Emit(new SimEvent("hooked_up").With("district", id).With("cost", d.Hookup));
            foreach (var ch in sim.Gd.Chapters)
            {
                if (ch.OpensWith == id && ch.Id > s.Chapter)
                {
                    s.Chapter = ch.Id;
                    s.ChapterShift = 0;
                    sim.Emit(new SimEvent("chapter").With("chapter", ch.Id));
                }
            }
            return true;
        }

        /// <summary>Everything buyable now (investments + hookups), cheapest first (ties: data order).</summary>
        public static List<Buyable> BuyableNow(Simulation sim)
        {
            var o = new List<Buyable>();
            foreach (var x in sim.Gd.Investments) if (WhyNot(sim, x.Id) == "") o.Add(new Buyable { Kind = "investment", Id = x.Id, Cost = x.Cost });
            foreach (var d in sim.Gd.Districts) if (HookupWhyNot(sim, d.Id) == "") o.Add(new Buyable { Kind = "hookup", Id = d.Id, Cost = d.Hookup });
            // stable: List.Sort is not, so sort an index list
            var idx = new List<int>();
            for (int i = 0; i < o.Count; i++) idx.Add(i);
            idx.Sort((a, b) => o[a].Cost != o[b].Cost ? o[a].Cost.CompareTo(o[b].Cost) : a.CompareTo(b));
            var r = new List<Buyable>();
            foreach (var i in idx) r.Add(o[i]);
            return r;
        }

        /// <summary>The report's money from a finished grid (whole $).</summary>
        public static void ShiftMoney(Simulation sim, Grid g, ShiftReport rep)
        {
            var gd = sim.Gd;
            long pm = PriceMilli(sim);
            var acc = g.Acc;
            double revenueBase = (acc.ServedWh + acc.FestWh * (double)g.FestPct / 100.0) / 1e6 * gd.TariffPerMwh;
            double fuelBase = 0;
            for (int i = 0; i < 6; i++)
            {
                double f = g.PFuel[i];
                if (i == Grid.PGas) f *= 1.0 + g.GasSpikePct / 100.0;
                fuelBase += g.PGenWh[i] / 1e6 * f;
            }
            fuelBase += acc.BackupWh / 1e6 * gd.BackupFuel;
            double upkeepBase = sim.S.Crews * gd.CrewUpkeep;
            foreach (var kv in sim.S.Plants)
                if (kv.Value > 0 && gd.Plant.TryGetValue(kv.Key, out var p)) upkeepBase += p.Upkeep[kv.Value - 1];
            double crewBase = acc.Jobs * gd.CrewJobCost;
            double drBase = acc.Dr * gd.DrCost;
            double m = pm / 1000.0;
            rep.Revenue = (long)Math.Round(revenueBase * m, MidpointRounding.AwayFromZero);
            rep.Bonus = g.TestFrom >= 0 && acc.TestDark == 0
                ? (long)Math.Round(revenueBase * m * gd.Event["hospital_test"].BonusShiftPct / 100.0, MidpointRounding.AwayFromZero) : 0;
            rep.Fuel = (long)Math.Round(fuelBase * m, MidpointRounding.AwayFromZero);
            rep.Upkeep = (long)Math.Round((upkeepBase + crewBase + drBase) * m, MidpointRounding.AwayFromZero);
            rep.Profit = rep.Revenue + rep.Bonus - rep.Fuel - rep.Upkeep;
            rep.PriceMilli = pm;
        }

        /// <summary>One day of current income (IAP.md: 6.7 x the last completed shift's net without bonuses, floor $1,000).</summary>
        public static long DayIncome(Simulation sim) =>
            Math.Max(sim.Gd.DayIncomeFloor, (long)Math.Round(sim.S.LastNet * sim.Gd.DayShifts, MidpointRounding.AwayFromZero));

        /// <summary>Night crew pay for the time away (cap 8 h = 0.7 shift; offline_plus 16 h x1.5). Adds the cash; null if nothing.</summary>
        public static OfflineSummary Offline(Simulation sim, long nowUnix)
        {
            var s = sim.S;
            long last = s.LastSeen;
            s.LastSeen = Math.Max(last, nowUnix);
            if (last <= 0 || nowUnix <= last) return null;
            long away = nowUnix - last;
            var gd = sim.Gd;
            if (away < gd.OfflineMinS) return null;
            bool plus = sim.Perk("offline_plus");
            long capS = (long)(plus ? gd.OfflinePlusCapH : gd.OfflineCapH) * 3600;
            double rate = plus ? gd.OfflinePlusRate : 1.0;
            long counted = Math.Min(away, capS);
            long net = Math.Max(0, s.LastNet);
            long cash = (long)Math.Round(net * gd.OfflineShare * counted / (gd.OfflineCapH * 3600.0) * rate, MidpointRounding.AwayFromZero);
            s.Cash += cash;
            s.Stats.Offline += cash;
            var sum = new OfflineSummary { AwayS = away, CountedS = counted, Cash = cash, Capped = away > capS, AwayDays = (int)(away / 86400) };
            s.OfflineLast = sum;
            sim.Emit(new SimEvent("offline").With("cash", cash).With("away_s", away).With("capped", sum.Capped).With("away_days", sum.AwayDays));
            return sum;
        }

        // ------------------------------------------------------------------------------------------- rewards

        static RewardState Rw(Simulation sim, long nowUnix)
        {
            var r = sim.S.Rewards;
            long day = nowUnix / 86400;
            if (r.Day != day)
            {
                r.Day = day;
                r.N.Clear();
                r.CashToday = 0;
            }
            return r;
        }

        public static long RewardCapSingle(Simulation sim) => (long)Math.Floor(DayIncome(sim) * sim.Gd.RewardSingleMaxDay);
        public static long RewardCapDaily(Simulation sim) => (long)Math.Floor(DayIncome(sim) * sim.Gd.RewardDailyMaxDay);

        /// <summary>Whether a placement can be offered now (daily cap, cooldown, context) and what it would give.</summary>
        public static RewardResult RewardOffer(Simulation sim, string placement, long nowUnix)
        {
            if (!sim.Gd.Placements.TryGetValue(placement ?? "", out var pl)) return new RewardResult { Reason = "unknown" };
            if (sim.S.TutorialSafe) return new RewardResult { Reason = "tutorial" };
            var r = Rw(sim, nowUnix);
            if ((r.N.TryGetValue(placement, out int used) ? used : 0) >= pl.Cap) return new RewardResult { Reason = "cap" };
            if (r.Last.TryGetValue(placement, out long last) && nowUnix - last < pl.CooldownS) return new RewardResult { Reason = "cooldown" };
            long amount = 0;
            var g = sim.Grid;
            switch (placement)
            {
                case "offline_x2":
                    var o = sim.S.OfflineLast;
                    if (o == null || o.Doubled || o.Cash <= 0) return new RewardResult { Reason = "context" };
                    amount = o.Cash;
                    break;
                case "shift_bonus":
                    if (sim.S.Phase != "report" || sim.S.Report == null || sim.S.Rewards.BonusShift == sim.S.ShiftNo) return new RewardResult { Reason = "context" };
                    amount = (long)Math.Round(Math.Max(0, sim.S.Report.Profit) * pl.Share, MidpointRounding.AwayFromZero);
                    break;
                case "emergency_import":
                    if (g == null || sim.S.Phase != "live" || g.NeedlePm >= 0 || g.ImportUntil > g.Step) return new RewardResult { Reason = "context" };
                    break;
                case "crew_overtime":
                    if (g == null || sim.S.Phase != "live" || g.OpenFaults().Count == 0) return new RewardResult { Reason = "context" };
                    break;
            }
            if (pl.Cash)
            {
                amount = Math.Min(amount, RewardCapSingle(sim));
                amount = Math.Min(amount, Math.Max(0, RewardCapDaily(sim) - r.CashToday));
                if (amount <= 0) return new RewardResult { Reason = "daily_cash_cap" };
            }
            return new RewardResult { Ok = true, Amount = amount };
        }

        /// <summary>Grants after the SDK "earned" callback (or without video for ad_free owners, same caps). fault: crew_overtime target.</summary>
        public static RewardResult GrantReward(Simulation sim, string placement, long nowUnix, int fault = -1)
        {
            var off = RewardOffer(sim, placement, nowUnix);
            if (!off.Ok) return off;
            var r = Rw(sim, nowUnix);
            switch (placement)
            {
                case "offline_x2": sim.S.OfflineLast.Doubled = true; break;
                case "shift_bonus":
                    sim.S.Rewards.BonusShift = sim.S.ShiftNo;
                    sim.S.Report.RewardBonus = off.Amount;
                    break;
                case "emergency_import": sim.Queue(Cmd.Import()); break;
                case "crew_overtime":
                    int fid = fault >= 0 ? fault : sim.Grid.OpenFaults()[0].Id;
                    if (!sim.Queue(Cmd.Overtime(fid)).Ok) return new RewardResult { Reason = "context" };
                    break;
            }
            r.N[placement] = (r.N.TryGetValue(placement, out int n) ? n : 0) + 1;
            r.Last[placement] = nowUnix;
            if (off.Amount > 0)
            {
                r.CashToday += off.Amount;
                sim.S.Cash += off.Amount;
                sim.S.Stats.Rewarded += off.Amount;
            }
            sim.Emit(new SimEvent("reward").With("placement", placement).With("amount", off.Amount).With("daily_index", r.N[placement]));
            return new RewardResult { Ok = true, Amount = off.Amount };
        }

        // ------------------------------------------------------------------------------------------- store

        public static string Sha256Hex(string text)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
                var sb = new StringBuilder(64);
                foreach (var b in bytes) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        /// <summary>Idempotent: the same store transaction id never pays twice (kept in the save, atomic with the cash).</summary>
        public static FundsResult GrantPurchaseFunds(Simulation sim, string txnId, string product)
        {
            if (string.IsNullOrEmpty(txnId) || !sim.Gd.ConsumableDays.TryGetValue(product ?? "", out double days))
                return new FundsResult { Reason = "unknown_product" };
            string key = Sha256Hex(txnId);
            if (sim.S.Receipts.Contains(key)) return new FundsResult { Ok = true, Duplicate = true };
            long amount = (long)Math.Round(DayIncome(sim) * days, MidpointRounding.AwayFromZero);
            sim.S.Receipts.Add(key);
            while (sim.S.Receipts.Count > sim.Gd.ReceiptsMax) sim.S.Receipts.RemoveAt(0);
            sim.S.Cash += amount;
            sim.S.Stats.BoughtFunds += amount;
            sim.Emit(new SimEvent("purchase_funds").With("product", product).With("amount", amount));
            return new FundsResult { Ok = true, Granted = amount };
        }
    }
}
