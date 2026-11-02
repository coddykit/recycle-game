// Volt City sim core — engine independent (no UnityEngine). C# 9 / .NET Standard 2.1.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace VoltCity.Sim
{
    public sealed class CareerRun
    {
        public string Policy = "";
        public long Seed;
        public List<int> Owned = new List<int>();          // investments owned at the end of each day
        public int DoneDay = -1, Ch5Done = -1, MaxNoBuy, NegMax, StormAfterOne, TutEvents, Shifts;
        public double MaxIdle;
        public long NetLast;
        public Dictionary<string, int[]> Ch = new Dictionary<string, int[]>();   // chapter -> [shifts, >=2 stars, 3 stars]

        public string ToJsonLine()
        {
            var own = new List<object>();
            foreach (var o in Owned) own.Add((long)o);
            var ch = new JObj();
            foreach (var kv in Ch) ch[kv.Key] = new List<object> { (long)kv.Value[0], (long)kv.Value[1], (long)kv.Value[2] };
            return Json.Write(new JObj
            {
                ["policy"] = Policy, ["seed"] = Seed, ["owned"] = own, ["done_day"] = (long)DoneDay, ["ch5_done"] = (long)Ch5Done, ["max_nobuy"] = (long)MaxNoBuy,
                ["max_idle"] = MaxIdle, ["ch"] = ch, ["neg_max"] = (long)NegMax, ["storm_after_1"] = (long)StormAfterOne, ["tut_events"] = (long)TutEvents,
                ["net_last"] = NetLast, ["shifts"] = (long)Shifts
            });
        }

        public static CareerRun FromJson(object o)
        {
            var r = new CareerRun
            {
                Policy = J.S(o, "policy"), Seed = J.L(o, "seed"), DoneDay = J.I(o, "done_day", -1), Ch5Done = J.I(o, "ch5_done", -1), MaxNoBuy = J.I(o, "max_nobuy"),
                MaxIdle = J.D(o, "max_idle"), NegMax = J.I(o, "neg_max"), StormAfterOne = J.I(o, "storm_after_1"), TutEvents = J.I(o, "tut_events"),
                NetLast = J.L(o, "net_last"), Shifts = J.I(o, "shifts")
            };
            r.Owned.AddRange(J.IntArr(o, "owned"));
            var ch = J.Obj(o, "ch");
            if (ch != null) foreach (var k in ch.Keys) r.Ch[k] = J.IntArr(ch, k);
            return r;
        }
    }

    public sealed class MatrixCell
    {
        public string Key = "", Best = "";
        public Dictionary<string, double> Payback = new Dictionary<string, double>();
    }

    /// <summary>
    /// Headless balance runner — DESIGN section 7 gates as real gates (a failing gate fails the run).
    /// Modes: "quick" (6 seeds per policy + 3-seed plant matrix; the check gate) · "full" (seeds 1..N, one shard of K/N,
    /// JSON lines to a file) · "merge" (aggregate shard files + 40-seed matrix + gates).
    /// Policies: greedy (the mixed reference bot), saver, renewables_first, gas_only, battery_only, never_shed, naive_coach,
    /// premium (premium + offline_plus perks), dispatcher (paid automation; pay-to-win check).
    /// </summary>
    public static class BalanceRunner
    {
        public static readonly Dictionary<string, int> CareerDays = new Dictionary<string, int>
        {
            ["greedy"] = 35, ["premium"] = 35, ["gas_only"] = 30, ["battery_only"] = 30, ["renewables_first"] = 40, ["saver"] = 40,
            ["never_shed"] = 8, ["naive_coach"] = 6, ["dispatcher"] = 35
        };

        /// <summary>Value of lost load in the plant matrix: unserved energy costs this many times its tariff (rating / hookup gates).</summary>
        public const double VollX = 4.0;
        public const long Epoch = 1800000000L;

        public static CareerRun Career(GameData gd, string policy, long seed)
        {
            var sim = Policies.NewCareer(gd, policy, seed * 7919 + 17);
            var run = new CareerRun { Policy = policy, Seed = seed };
            long now = Epoch;
            int nobuy = 0, prevStars = 3;
            int days = CareerDays.TryGetValue(policy, out int dd) ? dd : 35;
            for (int d = 1; d <= days; d++)
            {
                var r = Policies.PlayDay(sim, policy, now);
                now += 86400;
                run.Owned.Add(sim.S.Owned.Count);
                bool allDone = sim.S.Owned.Count >= gd.Investments.Count && sim.S.Hooked.Count >= gd.Districts.Count;
                if (run.DoneDay < 0)
                {
                    nobuy = r.Purchases > 0 ? 0 : nobuy + 1;
                    run.MaxNoBuy = Math.Max(run.MaxNoBuy, nobuy);
                    if (!allDone) run.MaxIdle = Math.Max(run.MaxIdle, r.IdleCashEnd / Math.Max(1.0, (double)r.Income));
                }
                if (allDone && run.DoneDay < 0) run.DoneDay = d;
                int from = Math.Max(0, sim.S.Reports.Count - 6);
                for (int i = from; i < sim.S.Reports.Count; i++)
                {
                    var rep = sim.S.Reports[i];
                    string c = rep.Chapter.ToString();
                    if (!run.Ch.TryGetValue(c, out var cnt)) run.Ch[c] = cnt = new int[3];
                    cnt[0]++;
                    if (rep.Stars >= 2) cnt[1]++;
                    if (rep.Stars == 3) cnt[2]++;
                    run.NegMax = Math.Max(run.NegMax, rep.Negative);
                    if (prevStars <= 1 && rep.Events.Contains("thunderstorm")) run.StormAfterOne++;
                    if (rep.Shift < gd.TutorialShifts)
                        foreach (var e in rep.Events) if (gd.Event.TryGetValue(e, out var ed) && ed.Negative) run.TutEvents++;
                    prevStars = rep.Stars;
                }
                run.Shifts = sim.S.ShiftNo;
                bool stop;
                switch (policy)
                {
                    case "greedy": case "premium": stop = allDone && sim.S.Ch5Done >= 0; break;
                    case "never_shed": stop = sim.S.Chapter >= 3; break;
                    case "naive_coach": stop = sim.S.Chapter >= 2; break;
                    default: stop = sim.S.Ch5Done >= 0; break;
                }
                if (stop) break;
            }
            run.Ch5Done = sim.S.Ch5Done;
            run.NetLast = sim.S.LastNet;
            return run;
        }

        // ------------------------------------------------------------------------------------------- plant matrix

        static readonly string[][] Mixes =
        {
            new[] { "sunny", "sunny" }, new[] { "heatwave", "hot", "heatwave" }, new[] { "cloudy", "cloudy", "cloud_front" }, new[] { "windy", "windy" },
            new[] { "fog_calm", "calm", "fog" }, new[] { "storm", "rainy", "thunderstorm" }, new[] { "cold_snap", "calm", "cold_snap" },
            new[] { "fuel_spike", "cloudy", "fuel_spike" }, new[] { "trips", "sunny", "plant_trip" }, new[] { "cold_spike", "calm", "cold_snap", "fuel_spike" }
        };

        /// <summary>Decision points: the greedy career at the start of each of its first 14 days.</summary>
        public static List<KeyValuePair<string, string>> DecisionStates(GameData gd)
        {
            var o = new List<KeyValuePair<string, string>>();
            var sim = new Simulation(gd, 4242);
            long now = Epoch;
            for (int d = 1; d <= 14; d++)
            {
                o.Add(new KeyValuePair<string, string>("d" + d.ToString("00") + "ch" + sim.S.Chapter, SaveCodec.Encode(sim)));
                Policies.PlayDay(sim, "greedy", now);
                now += 86400;
            }
            return o;
        }

        static string NextLevel(GameData gd, Simulation sim, string type)
        {
            int lv = sim.S.Level(type);
            foreach (var x in gd.Investments)
            {
                if (x.Kind != "plant" || x.Target != type || x.Level != lv + 1) continue;
                var p = gd.Plant[type];
                if (!string.IsNullOrEmpty(p.NeedsDistrict) && !sim.S.IsHooked(p.NeedsDistrict)) return "";
                return x.Id;
            }
            return "";
        }

        static double Profit(GameData gd, string save, string buyId, string[] mix, int seeds)
        {
            double tot = 0;
            for (int sd = 0; sd < seeds; sd++)
            {
                var sim = SaveCodec.Decode(save, gd).Sim;
                sim.S.Seed = 1000 + sd * 37;
                sim.S.Cash = 100000000;
                if (buyId != "")
                {
                    sim.Buy(buyId);
                    sim.S.Owned.Remove(buyId);   // compare at the base contract level: plant value only
                }
                var force = new List<string>();
                for (int i = 2; i < mix.Length; i++) force.Add(mix[i]);
                Policies.PlayShift(sim, "greedy", mix[1], force);
                var rep = sim.S.Report;
                double unserved = rep.DemandMwh - rep.ServedMwh;
                tot += rep.Profit - unserved * gd.TariffPerMwh * VollX * rep.PriceMilli / 1000.0;
            }
            return tot / seeds;
        }

        /// <summary>Best next plant buy (payback = cost / extra value per shift) per decision point x weather mix.</summary>
        public static List<MatrixCell> Matrix(GameData gd, int seeds)
        {
            var cells = new List<MatrixCell>();
            foreach (var st in DecisionStates(gd))
            {
                var probe = SaveCodec.Decode(st.Value, gd).Sim;
                foreach (var mix in Mixes)
                {
                    var cell = new MatrixCell { Key = st.Key + "|" + mix[0] };
                    double baseV = Profit(gd, st.Value, "", mix, seeds);
                    foreach (var t in GameData.Plants)
                    {
                        string id = NextLevel(gd, probe, t);
                        if (id == "") continue;
                        double gain = Profit(gd, st.Value, id, mix, seeds) - baseV;
                        cell.Payback[t] = gain > 0 ? Math.Round(gd.Investment[id].Cost / gain, 1) : 9999.0;
                    }
                    // a cell where no next level pays back (every gain <= 0) has no winner
                    foreach (var kv in cell.Payback)
                        if (kv.Value < 9999.0 && (cell.Best == "" || kv.Value < cell.Payback[cell.Best])) cell.Best = kv.Key;
                    cells.Add(cell);
                }
            }
            return cells;
        }

        // ------------------------------------------------------------------------------------------- gates

        static double Pct(List<double> a, double q)
        {
            if (a.Count == 0) return -1;
            var s = new List<double>(a);
            s.Sort();
            int i = (int)(q * (s.Count - 1) + 0.5);
            return s[Math.Max(0, Math.Min(s.Count - 1, i))];
        }

        static List<CareerRun> By(List<CareerRun> runs, string policy) => runs.FindAll(r => r.Policy == policy);

        public sealed class Gate
        {
            public string Label;
            public bool Ok;
        }

        public static List<Gate> Evaluate(GameData gd, List<CareerRun> runs, List<MatrixCell> cells, StringBuilder log)
        {
            var prevCulture = System.Threading.Thread.CurrentThread.CurrentCulture;
            System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
            try { return EvaluateInvariant(gd, runs, cells, log); }
            finally { System.Threading.Thread.CurrentThread.CurrentCulture = prevCulture; }
        }

        static List<Gate> EvaluateInvariant(GameData gd, List<CareerRun> runs, List<MatrixCell> cells, StringBuilder log)
        {
            var gates = new List<Gate>();
            void G(string label, bool ok) => gates.Add(new Gate { Label = label, Ok = ok });
            var g = By(runs, "greedy");
            var pr = By(runs, "premium");
            log.AppendLine("runs: " + runs.Count + " (greedy " + g.Count + ")");
            log.AppendLine("| day | owned median (of 50) | p5 | p95 |");
            foreach (int d in new[] { 1, 3, 7, 10, 14, 21, 25 })
            {
                var v = g.ConvertAll(r => (double)r.Owned[Math.Min(d, r.Owned.Count) - 1]);
                log.AppendLine($"| {d} | {Pct(v, 0.5)} | {Pct(v, 0.05)} | {Pct(v, 0.95)} |");
            }
            var d7 = g.ConvertAll(r => (double)r.Owned[Math.Min(7, r.Owned.Count) - 1]);
            var done = g.ConvertAll(r => r.DoneDay > 0 ? (double)r.DoneDay : 99.0);
            G($"normal day 7 <= 70 % (35/50) in every run (max {Pct(d7, 1)}, median {Pct(d7, 0.5)})", Pct(d7, 1) <= 35);
            G($"normal day 7 >= 45 % (22/50), not a wall (min {Pct(d7, 0)})", Pct(d7, 0) >= 22);
            int inWin = done.FindAll(x => x >= 25 && x <= 30).Count;
            G($"normal: everything owned on day 25-30 in >= 95 % of runs ({100.0 * inWin / Math.Max(1, done.Count):0.0} %, median {Pct(done, 0.5)}, p5 {Pct(done, 0.05)}, p95 {Pct(done, 0.95)})",
                inWin >= 0.95 * done.Count);
            var nb = g.ConvertAll(r => (double)r.MaxNoBuy);
            G($"normal: never 2 days in a row without a purchase (longest {Pct(nb, 1)})", Pct(nb, 1) <= 1);
            var idle = g.ConvertAll(r => r.MaxIdle);
            G($"normal: idle cash at session end <= ~1 day of income (p95 {Pct(idle, 0.95):0.00}, max {Pct(idle, 1):0.00}; limit 1.25)", Pct(idle, 0.95) <= 1.25);
            var p7 = pr.ConvertAll(r => (double)r.Owned[Math.Min(7, r.Owned.Count) - 1]);
            var pdone = pr.ConvertAll(r => r.DoneDay > 0 ? (double)r.DoneDay : 99.0);
            G($"premium day 7 <= 78 % (39/50) (max {Pct(p7, 1)}, median {Pct(p7, 0.5)})", Pct(p7, 1) <= 39);
            G($"premium: not everything before day 19 (min {Pct(pdone, 0)}, median {Pct(pdone, 0.5)})", Pct(pdone, 0) >= 19);
            var med = new Dictionary<string, double>();
            foreach (var p in new[] { "greedy", "gas_only", "battery_only", "renewables_first", "saver", "dispatcher" })
            {
                var v = By(runs, p).ConvertAll(r => r.Ch5Done >= 0 ? (double)r.Ch5Done : 999.0);
                med[p] = Pct(v, 0.5);
                log.AppendLine($"chapter 5 finished (shift) {p,-17} median {med[p]} p95 {Pct(v, 0.95)}");
            }
            G($"gas-only finishes chapter 5 >= 20 % slower than the mixed bot ({med["gas_only"]} vs {med["greedy"]})", med["gas_only"] >= 1.2 * med["greedy"]);
            G($"battery-only finishes chapter 5 >= 20 % slower than the mixed bot ({med["battery_only"]} vs {med["greedy"]})", med["battery_only"] >= 1.2 * med["greedy"]);
            G($"dispatcher (paid automation) within +-10 % of the hand-played bot ({med["dispatcher"]} vs {med["greedy"]})", Math.Abs(med["dispatcher"] - med["greedy"]) <= 0.1 * med["greedy"]);
            G($"renewables-first and saver finish chapter 5, no wall ({med["renewables_first"]} / {med["saver"]})", med["renewables_first"] < 999 && med["saver"] < 999);
            int[] ns = new int[2], nv = new int[2];
            foreach (var r in By(runs, "never_shed"))
                foreach (var c in new[] { "1", "2" })
                    if (r.Ch.TryGetValue(c, out var x)) { ns[0] += x[0]; ns[1] += x[1]; }
            G($"never-shed (no manual shedding) gets >= 2 stars in >= 80 % of chapter 1-2 shifts ({100.0 * ns[1] / Math.Max(1, ns[0]):0.0} % of {ns[0]})", ns[1] >= 0.8 * ns[0]);
            foreach (var r in By(runs, "naive_coach"))
                if (r.Ch.TryGetValue("1", out var x)) { nv[0] += x[0]; nv[1] += x[1]; }
            G($"naive coach player gets >= 2 stars in >= 80 % of chapter-1 shifts ({100.0 * nv[1] / Math.Max(1, nv[0]):0.0} % of {nv[0]})", nv[1] >= 0.8 * nv[0]);
            int negMax = 0, sa = 0, te = 0;
            foreach (var r in runs) { negMax = Math.Max(negMax, r.NegMax); sa += r.StormAfterOne; te += r.TutEvents; }
            G($"<= 2 negative events per shift (max {negMax})", negMax <= 2);
            G($"no thunderstorm right after a 1-star shift ({sa})", sa == 0);
            G($"no bad events in the tutorial shifts ({te})", te == 0);
            var wins = new Dictionary<string, int>();
            log.AppendLine("| cell | best next plant | payback (shifts) per type |");
            foreach (var c in cells)
            {
                if (c.Best != "") wins[c.Best] = (wins.TryGetValue(c.Best, out int w) ? w : 0) + 1;
                var parts = new List<string>();
                foreach (var kv in c.Payback) parts.Add(kv.Key + " " + kv.Value);
                log.AppendLine($"| {c.Key} | {c.Best} | {string.Join(", ", parts)} |");
            }
            var missing = new List<string>();
            foreach (var t in GameData.Plants) if (!wins.ContainsKey(t)) missing.Add(t);
            var winText = new List<string>();
            foreach (var kv in wins) winText.Add(kv.Key + " " + kv.Value);
            log.AppendLine("cell wins: " + string.Join(", ", winText));
            G($"no dominant plant: every plant type is the best next buy in >= 1 decision/weather cell (missing [{string.Join(",", missing)}])", missing.Count == 0);
            int top = 0;
            foreach (var kv in wins) top = Math.Max(top, kv.Value);
            int decided = cells.FindAll(c => c.Best != "").Count;
            G($"no plant type wins > 50 % of the decided cells (top {top} / {decided}; {cells.Count - decided} cells where no plant pays back)", top <= 0.5 * decided);
            int failed = 0;
            foreach (var gt in gates)
            {
                log.AppendLine((gt.Ok ? "GATE ok   " : "GATE FAIL ") + gt.Label);
                if (!gt.Ok) failed++;
            }
            log.AppendLine(failed == 0 ? "BALANCE PASSED" : "BALANCE FAILED: " + failed + " gates");
            return gates;
        }

        /// <summary>All policies x seeds 1..n (only seeds with seed % shardN == shardK).</summary>
        public static List<CareerRun> Careers(GameData gd, int seeds, int shardK = 0, int shardN = 1, Action<CareerRun> each = null)
        {
            var runs = new List<CareerRun>();
            for (int sd = 1; sd <= seeds; sd++)
            {
                if (sd % shardN != shardK) continue;
                foreach (var p in CareerDays.Keys)
                {
                    var r = Career(gd, p, sd);
                    runs.Add(r);
                    each?.Invoke(r);
                }
            }
            return runs;
        }

        /// <summary>CLI entry (Unity -executeMethod wrapper or a plain .NET host): quick | full shard=K/N seeds=N out=F | merge F1 F2 …
        /// data=DIR points at the data folder. Returns 0 when every gate holds.</summary>
        public static int Cli(string[] args, Action<string> print)
        {
            string mode = args.Length > 0 ? args[0] : "quick";
            string dataDir = "Assets/Resources/data", outPath = "balance_shard.jsonl";
            int k = 0, n = 1, seeds = 1000;
            var files = new List<string>();
            for (int i = 1; i < args.Length; i++)
            {
                string a = args[i];
                if (a.StartsWith("data=", StringComparison.Ordinal)) dataDir = a.Substring(5);
                else if (a.StartsWith("out=", StringComparison.Ordinal)) outPath = a.Substring(4);
                else if (a.StartsWith("seeds=", StringComparison.Ordinal)) seeds = int.Parse(a.Substring(6));
                else if (a.StartsWith("shard=", StringComparison.Ordinal)) { var p = a.Substring(6).Split('/'); k = int.Parse(p[0]); n = int.Parse(p[1]); }
                else files.Add(a);
            }
            var gd = GameData.LoadDirectory(dataDir);
            if (!gd.Ok) { print("data errors: " + string.Join("; ", gd.Errors)); return 2; }
            var log = new StringBuilder();
            List<Gate> gates;
            switch (mode)
            {
                case "full":
                    using (var w = new StreamWriter(outPath))
                        Careers(gd, seeds, k, n, r => { w.WriteLine(r.ToJsonLine()); w.Flush(); });
                    print("wrote " + outPath);
                    return 0;
                case "frame":
                {
                    // ASCII debug frame of one storm shift (docs/P1-VALIDATION.md): a crew on its way + the hour-by-hour strip
                    var sim = SimProbe.CareerAfterDays(gd, 2026, 3);
                    sim.StartShift("rainy", new List<string> { "thunderstorm", "plant_trip" });
                    bool shown = false;
                    Grid last = null;
                    while (sim.Phase == "live")
                    {
                        SimProbe.DriveStep(sim);
                        if (sim.Grid == null) break;
                        last = sim.Grid;
                        if (!shown && sim.Grid.OpenFaults().Count > 0 && sim.Grid.Crews.Exists(c => c.State == CrewTruck.Travel))
                        {
                            print(AsciiFrame.Frame(sim));
                            shown = true;
                        }
                    }
                    print(AsciiFrame.Strip(last));
                    print("report " + Json.Write(sim.S.Report.ToJson()));
                    return 0;
                }
                case "curve":
                    // day-by-day table of one policy (default greedy): owned, net per shift, idle cash / day income, rating
                    string pol = files.Count > 0 ? files[0] : "greedy";
                    foreach (int sd in new[] { 1, 2, 3 })
                    {
                        var sim = Policies.NewCareer(gd, pol, sd * 7919 + 17);
                        long now = Epoch;
                        print($"{pol} seed {sd}: day owned hooked chapter net/shift idle(day income) rating");
                        for (int d = 1; d <= 30; d++)
                        {
                            var r = Policies.PlayDay(sim, pol, now);
                            now += 86400;
                            double idle = r.IdleCashEnd / Math.Max(1.0, (double)r.Income);
                            print($"  {d,2} {sim.S.Owned.Count,2} {sim.S.Hooked.Count} {sim.S.Chapter} {r.NetLast,7} {idle.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)} {sim.S.RatingX100}");
                            if (sim.S.Owned.Count >= gd.Investments.Count && sim.S.Hooked.Count >= gd.Districts.Count) break;
                        }
                    }
                    return 0;
                case "merge":
                    var runs = new List<CareerRun>();
                    foreach (var f in files)
                        foreach (var line in File.ReadAllLines(f))
                            if (line.Trim() != "") runs.Add(CareerRun.FromJson(Json.Parse(line)));
                    gates = Evaluate(gd, runs, Matrix(gd, 40), log);
                    break;
                default:
                    gates = Evaluate(gd, Careers(gd, 6), Matrix(gd, 3), log);
                    break;
            }
            print(log.ToString());
            return gates.TrueForAll(x => x.Ok) ? 0 : 1;
        }
    }
}
