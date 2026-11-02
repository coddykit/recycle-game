// Volt City — play diagnostics (#183): three headless player profiles x 6 shifts x 3 seeds, every sim event with its shift,
// real-time minute (1x) and game hour, plus two detectors:
//   UNFAIR  a shed / plant trip / line trip that the CAUTIOUS player also suffers at the same moment (nobody could avoid it),
//           or that lands < 10 s after its cause (fault / event start / weather jump) — no time to react;
//   BORING  >= 60 real seconds (1x) with no event, the needle inside the band and the cautious operator issuing no command.
// First 10 minutes: the guided shift + the next shifts at 1x of a fresh career, decisions and events per minute.
// EXPLICIT (not in the suite): VOLT_DIAG_OUT=<file> dotnet test --filter Name~PlayDiagnostics (game_dotnet_check's project).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using VoltCity.Game;
using VoltCity.Sim;

namespace VoltCity.Game.Tests
{
    public class PlayDiagnostics
    {
        static GameData gd;

        [OneTimeSetUp]
        public void Load()
        {
            string env = Environment.GetEnvironmentVariable("VOLTCITY_UNITY");
            string unity = !string.IsNullOrEmpty(env) ? env : Directory.GetCurrentDirectory();
            gd = GameData.LoadDirectory(Path.Combine(unity, "Assets", "Resources", "data"));
            Assert.IsTrue(gd.Ok, string.Join("; ", gd.Errors));
        }

        sealed class ShiftLog
        {
            public string Who; public long Seed; public int Shift, Stars, UptimePm, Decisions; public string Weather = "";
            public readonly List<(int step, string type, string detail)> Events = new List<(int, string, string)>();
            public readonly List<(int from, int to)> Calm = new List<(int, int)>();
        }

        static double Min(int step) => step * 0.25 / 60.0;   // real minutes at 1x (0.25 s per step)

        static ShiftLog Play(Simulation sim, string who, Random rng, bool guided)
        {
            var log = new ShiftLog { Who = who, Shift = sim.S.ShiftNo };
            sim.TutorialSafe = guided;
            if (!sim.StartShift()) return null;
            log.Weather = sim.S.Plan.Weather + " ch" + sim.S.Chapter + " plan[" + string.Join(",", sim.S.Plan.Events.Select(e => e.Id)) + "]";
            int calmFrom = 0, look = who == "cautious" ? 4 : who == "typical" ? 16 : 8;
            int guard = 0;
            while (sim.Phase == "live" && guard++ < 5000)
            {
                var g = sim.Grid;
                int before = sim.S.Pending.Count;
                if (g.Step % look == 0)
                {
                    if (who == "cautious" || guided)
                        foreach (var c in Dispatcher.Operate(sim, Policies.OperatorFor("greedy"))) sim.Queue(c);
                    else if (who == "typical" && rng.NextDouble() < 0.65)
                    {
                        if (g.NeedlePm < -50) Up(sim, 1);
                        else if (g.NeedlePm > 60) Down(sim, 1);
                        else { var a = Coach.AssistFor(sim); if (a != null && a.Value.key == "coach.assist_restore") Up(sim, 1); }
                        var open = g.OpenFaults();
                        if (open.Count > 0 && rng.NextDouble() < 0.5) sim.Queue(Cmd.SendCrew(open[0].Id));
                    }
                    else if (who == "reckless")
                    {
                        // over-reacts (two taps at a small offset), toggles the peaker, now and then sheds by hand
                        if (g.NeedlePm < -20) Up(sim, 2);
                        else if (g.NeedlePm > 20) Down(sim, 2);
                        if (rng.NextDouble() < 0.05) sim.Queue(Cmd.Power(g.POn[Grid.PGas] == 0));
                        if (g.NeedlePm < -60 && rng.NextDouble() < 0.3)
                        {
                            for (int i = 0; i < g.DIds.Length; i++) if (g.IsLit(i) && i != g.Hospital) { sim.Queue(Cmd.Shed(g.DIds[i])); break; }
                        }
                        var open = g.OpenFaults();
                        if (open.Count > 0) sim.Queue(Cmd.SendCrew(open[0].Id));
                    }
                }
                int queued = sim.S.Pending.Count - before;
                log.Decisions += Math.Max(0, queued);
                sim.RunSteps(1);
                var ev = sim.DrainEvents();
                foreach (var e in ev)
                {
                    string d = e.Data != null && e.Data.ContainsKey("district") ? e.Data["district"]?.ToString() : e.Data != null && e.Data.ContainsKey("id") ? e.Data["id"]?.ToString() : "";
                    if (e.Type == "shift_end" || e.Type == "crew_arrived") continue;
                    log.Events.Add((g.Step, e.Type, d));
                }
                bool calm = ev.Count == 0 && Math.Abs(g.NeedlePm) <= gd.BandPct * 10 && queued <= 0;
                if (!calm)
                {
                    if (g.Step - calmFrom >= 240) log.Calm.Add((calmFrom, g.Step));   // 240 steps = 60 s at 1x
                    calmFrom = g.Step + 1;
                }
            }
            sim.DrainEvents();
            if (sim.Grid != null && sim.Grid.Step - calmFrom >= 240) log.Calm.Add((calmFrom, sim.Grid.Step));
            log.Stars = sim.S.Report?.Stars ?? 0;
            log.UptimePm = sim.S.Report?.UptimePm ?? 0;
            sim.Collect();
            Policies.BuyPhase(sim, "greedy");
            return log;
        }

        static void Up(Simulation sim, int n)
        {
            var g = sim.Grid;
            if (g.PLevel[Grid.PSteam] > 0 && g.SetpointAfterPending(Grid.PSteam) < 10) sim.Queue(Cmd.Ramp("steam", +1, n));
            else if (g.PLevel[Grid.PGas] > 0 && g.POn[Grid.PGas] == 0) sim.Queue(Cmd.Power(true));
            else if (g.PLevel[Grid.PGas] > 0) sim.Queue(Cmd.Ramp("gas", +1, n));
        }

        static void Down(Simulation sim, int n)
        {
            var g = sim.Grid;
            if (g.PLevel[Grid.PGas] > 0 && g.POn[Grid.PGas] == 1 && g.SetpointAfterPending(Grid.PGas) > 0) sim.Queue(Cmd.Ramp("gas", -1, n));
            else if (g.SetpointAfterPending(Grid.PSteam) > 0) sim.Queue(Cmd.Ramp("steam", -1, n));
        }

        static List<ShiftLog> PlayAll()
        {
            var logs = new List<ShiftLog>();
            foreach (long seed in new long[] { 3, 7, 11 })
                foreach (var who in new[] { "cautious", "typical", "reckless" })
                {
                    var sim = new Simulation(gd, seed);
                    var rng = new Random((int)seed * 31 + who.Length);
                    var first = Play(sim, who, rng, true);    // the guided first shift, as the game runs it
                    if (first != null) { first.Seed = seed; first.Who = who + "/guided"; logs.Add(first); }
                    for (int k = 0; k < 6; k++)
                    {
                        var l = Play(sim, who, rng, false);
                        if (l == null) break;
                        l.Seed = seed;
                        logs.Add(l);
                    }
                }
            return logs;
        }

        /// <summary>Patron #183-2c: a repaired district's load comes back over ~10 s (cold-load pickup), so the typical
        /// player never drops to 1 star from a repair rebound (a shed within 30 s of a repair) — 0 / 18 shifts.</summary>
        [Test]
        public void Typical_NoOneStarFromRepairRebound()
        {
            var bad = PlayAll().Where(l => l.Who == "typical" && l.Stars == 1 && l.Events.Any(e => e.type == "shed"
                          && l.Events.Any(r => r.type == "repaired" && r.step <= e.step && r.step >= e.step - 120))).ToList();
            Assert.IsEmpty(bad, string.Join("\n", bad.Select(l => $"seed {l.Seed} shift {l.Shift} lights {l.UptimePm / 10.0:F1} %")));
        }

        [Test, Explicit("diagnostic report")]
        public void Report()
        {
            var sb = new StringBuilder();
            var logs = PlayAll();
            // star table
            sb.AppendLine("STARS profile x shift (seeds 3 / 7 / 11), shifts 2-7 after the guided one");
            foreach (var who in new[] { "cautious", "typical", "reckless" })
            {
                var row = new List<string>();
                for (int sh = 1; sh <= 6; sh++)
                    row.Add(string.Join("/", logs.Where(l => l.Who == who && l.Shift == sh).Select(l => l.Stars)));
                var all = logs.Where(l => l.Who == who).ToList();
                sb.AppendLine($"  {who,-9} | {string.Join(" | ", row)} | mean {all.Average(l => l.Stars):F2} lights-on {all.Average(l => l.UptimePm) / 10:F1} %");
            }
            // unfair: losses the cautious player also took at the same step, or < 10 s after a cause
            sb.AppendLine("UNFAIR candidates (shed / trip):");
            var losses = new[] { "shed", "plant_trip", "line_trip" };
            foreach (var l in logs.Where(x => !x.Who.EndsWith("guided")))
                foreach (var e in l.Events.Where(e => losses.Contains(e.type)))
                {
                    if (e.type == "shed" && l.Events.Any(x => x.step == e.step && x.type == "shed" && x.detail == e.detail && l.Who == "reckless")) { }
                    var cause = l.Events.LastOrDefault(x => x.step < e.step && x.step >= e.step - 40 && (x.type == "fault" || x.type == "event_start" || x.type == "plant_trip" || x.type == "line_trip"));
                    var cautious = logs.FirstOrDefault(x => x.Who == "cautious" && x.Seed == l.Seed && x.Shift == l.Shift);
                    bool everyone = cautious != null && cautious.Events.Any(x => x.type == e.type && Math.Abs(x.step - e.step) <= 8);
                    if (everyone || cause.type != null)
                        sb.AppendLine($"  {l.Who,-9} seed {l.Seed} shift {l.Shift} min {Min(e.step):F1} {e.type} {e.detail}" +
                                      (everyone ? " [cautious too]" : "") + (cause.type != null ? $" [{(e.step - cause.step) * 0.25:F0} s after {cause.type} {cause.detail}]" : ""));
                }
            sb.AppendLine("ONE-STAR shifts of the typical player (timeline):");
            foreach (var l in logs.Where(x => x.Who == "typical" && x.Stars == 1))
                sb.AppendLine($"  seed {l.Seed} shift {l.Shift} lights {l.UptimePm / 10.0:F1} %: " + string.Join(", ", l.Events.Where(e => e.type != "crew_sent").Select(e => $"{Min(e.step):F1}m {e.type} {e.detail}")));
            sb.AppendLine("BORING stretches >= 60 s (1x), cautious player:");
            foreach (var l in logs.Where(x => x.Who == "cautious"))
                foreach (var c in l.Calm)
                    sb.AppendLine($"  seed {l.Seed} shift {l.Shift} min {Min(c.from):F1}-{Min(c.to):F1} ({(c.to - c.from) * 0.25:F0} s) weather {l.Weather}");
            // first 10 minutes (typical): guided shift (4 min) + shift 2 (4 min) + 2 min of shift 3
            sb.AppendLine("FIRST 10 MINUTES (typical, 1x): events + decisions per real minute");
            foreach (long seed in new long[] { 3, 7, 11 })
            {
                var ls = logs.Where(x => x.Seed == seed && x.Who.StartsWith("typical")).OrderBy(x => x.Shift).Take(3).ToList();
                var perMin = new List<string>();
                double off = 0;
                foreach (var l in ls)
                {
                    for (int m = 0; m < 4 && off + m < 10; m++)
                    {
                        int ev = l.Events.Count(e => Min(e.step) >= m && Min(e.step) < m + 1 && e.type != "crew_sent");
                        perMin.Add($"{off + m:F0}:{ev}");
                    }
                    off += 4;
                }
                sb.AppendLine($"  seed {seed}: {string.Join(" ", perMin)} | stars {string.Join(" ", ls.Select(x => x.Stars))} | decisions {string.Join(" ", ls.Select(x => x.Decisions))}");
            }
            sb.AppendLine("PLANS (seed / profile cautious): " + string.Join(" ", logs.Where(x => x.Who == "cautious").Select(x => $"s{x.Seed}#{x.Shift}:{x.Weather}")));
            sb.AppendLine("EVENTS by type (all non-guided shifts):");
            foreach (var g in logs.Where(x => !x.Who.EndsWith("guided")).SelectMany(x => x.Events).GroupBy(e => e.type).OrderByDescending(g => g.Count()))
                sb.AppendLine($"  {g.Key}: {g.Count()}");
            string outFile = Environment.GetEnvironmentVariable("VOLT_DIAG_OUT") ?? "";
            if (outFile != "") File.WriteAllText(outFile, sb.ToString());
            TestContext.WriteLine(sb.ToString());
        }
    }
}
