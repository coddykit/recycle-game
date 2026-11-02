// Volt City sim core — engine independent (no UnityEngine). C# 9 / .NET Standard 2.1.
using System;
using System.Text;

namespace VoltCity.Sim
{
    /// <summary>Text debug views of a live shift (tests, logs, docs/P1-VALIDATION.md).</summary>
    public static class AsciiFrame
    {
        static readonly string[] Short = { "HAR", "RIV", "HIL", "MKT", "TEC", "STA", "HOS", "AIR" };
        static readonly string[] StateMark = { "#", "-", "x", "B" };   // lit, auto-shed, hand-shed, hospital backup
        static readonly string[] CrewState = { "idle", "driving", "repairing", "returning" };

        /// <summary>One frame: clock, needle gauge, plants, storage, districts, lines, faults, crews.</summary>
        public static string Frame(Simulation sim) => Invariant(() => FrameText(sim));

        /// <summary>The whole shift hour by hour (demand / supply bars + needle + lit districts).</summary>
        public static string Strip(Grid g) => Invariant(() => StripText(g));

        static string Invariant(Func<string> f)
        {
            var prev = System.Threading.Thread.CurrentThread.CurrentCulture;
            System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
            try { return f(); }
            finally { System.Threading.Thread.CurrentThread.CurrentCulture = prev; }
        }

        static string FrameText(Simulation sim)
        {
            var g = sim.Grid;
            var sb = new StringBuilder();
            if (g == null) return "(no live shift: " + sim.Phase + ")";
            double h = g.HourOfDay;
            int hh = (int)h, mm = (int)((h - hh) * 60);
            sb.AppendLine($"VOLT CITY  shift {sim.S.ShiftNo}  ch {sim.S.Chapter}  {hh:00}:{mm:00}  step {g.Step}/{g.Steps}  weather {g.Plan.Weather}  cash ${sim.S.Cash}");
            int pos = Math.Max(-10, Math.Min(10, g.NeedlePm / 10));
            var gauge = new char[21];
            for (int i = 0; i < 21; i++) gauge[i] = Math.Abs(i - 10) <= 3 ? '=' : '.';
            gauge[pos + 10] = '|';
            sb.AppendLine($"NEEDLE  [{new string(gauge)}]  {g.NeedlePm / 10.0:+0.0;-0.0} %   demand {g.LastDemand / 1000.0:0.0} MW  supply {g.LastSupply / 1000.0:0.0} MW");
            for (int p = 0; p < 6; p++)
            {
                if (g.PLevel[p] == 0) continue;
                string extra = "";
                if (p <= Grid.PHydro) extra = $"set {g.SetpointAfterPending(p)}/10" + (g.PTrip[p] > g.Step ? "  TRIPPED" : "") + (p == Grid.PGas && g.POn[p] == 0 ? "  off" : "");
                if (p == Grid.PHydro) extra += $"  reservoir {(g.ResCapWh > 0 ? 100 * g.ResWh / g.ResCapWh : 0)} %";
                if (p == Grid.PBattery) extra = $"{Grid.Modes[g.BatMode]}  soc {(g.BatCapWh > 0 ? 100 * g.BatSoc / g.BatCapWh : 0)} %  out {g.BatOut / 1000.0:0.0} MW";
                int outKw = p <= Grid.PHydro ? g.POut[p] : (p == Grid.PWind ? (int)(g.PCap[p] * g.WindCf[Math.Min(g.Step, g.Steps - 1)]) : p == Grid.PSolar ? (int)(g.PCap[p] * g.SolarCf[Math.Min(g.Step, g.Steps - 1)]) : g.BatOut);
                sb.AppendLine($"  {GameData.Plants[p],-8} L{g.PLevel[p]} {outKw / 1000.0,6:0.0}/{g.PCap[p] / 1000.0,5:0} MW  {extra}");
            }
            var line = new StringBuilder("DISTRICTS ");
            for (int i = 0; i < g.DIds.Length; i++)
            {
                if (!g.DHooked[i]) { line.Append(Short[i % Short.Length]).Append(":   "); continue; }
                string mark = g.DState[i] == Grid.Lit && g.Route(i) < 0 ? "o" : StateMark[g.DState[i]];
                line.Append(Short[i % Short.Length]).Append(':').Append(mark).Append(g.DPart[i] > 0 ? "~" : " ").Append("  ");
            }
            sb.AppendLine(line.ToString());
            var ln = new StringBuilder("LINES     ");
            for (int c = 0; c < g.CIds.Length; c++)
            {
                if (!g.DHooked[Math.Max(0, Array.IndexOf(g.DIds, g.Gd.Corridors[c].District))]) continue;
                string st = g.CFault[c] >= 0 ? "FAULT" : (g.COutUntil[c] > g.Step ? "TRIP" : $"{Math.Abs(g.CFlow[c]) * 100 / Math.Max(1, g.CCap[c])}%");
                ln.Append(g.CIds[c].Substring(2, Math.Min(3, g.CIds[c].Length - 2))).Append(' ').Append(st).Append(g.CHeat[c] > Grid.HeatFull / 2 ? "!" : "").Append("  ");
            }
            sb.AppendLine(ln.ToString());
            foreach (var f in g.OpenFaults()) sb.AppendLine($"  fault #{f.Id} {f.Kind} at {f.Site} ({f.Cause}) crew {(f.Crew >= 0 ? f.Crew.ToString() : "-")}");
            foreach (var c in g.Crews) sb.AppendLine($"  truck {c.Id} {CrewState[c.State]} at {c.Node}" + (c.State != 0 ? $" until step {c.Until}" : ""));
            return sb.ToString();
        }

        static string StripText(Grid g)
        {
            var sb = new StringBuilder();
            sb.AppendLine("hour  demand MW  supply MW  needle  lit districts");
            for (int k = 0; k < g.Hourly.Count; k++)
            {
                var row = g.Hourly[k];
                int hour = (int)((g.Plan.StartMin / 60 + k) % 24);
                var lit = new StringBuilder();
                for (int i = 0; i < g.DIds.Length; i++) if (g.DHooked[i]) lit.Append((row[3] & (1 << i)) != 0 ? Short[i] + " " : "--- ");
                sb.AppendLine($"{hour:00}:00 {row[0] / 1000.0,9:0.0} {row[1] / 1000.0,10:0.0} {row[2] / 10.0,6:+0.0;-0.0}%  {lit}");
            }
            return sb.ToString();
        }
    }
}
