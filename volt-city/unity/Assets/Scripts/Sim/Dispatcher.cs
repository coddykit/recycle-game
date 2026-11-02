// Volt City sim core — engine independent (no UnityEngine). C# 9 / .NET Standard 2.1.
using System;
using System.Collections.Generic;

namespace VoltCity.Sim
{
    /// <summary>What an operator does in a decision (setpoints, battery, crews, manual shedding, Demand Response).</summary>
    public sealed class OperatorQ
    {
        public bool Setpoints = true, Battery = true, Crew = true, Shed = true, DemandResponse = true;
        public int Taps = 3;
    }

    /// <summary>
    /// Operator logic shared by the dispatcher assistant (auto-baseload / auto-battery / auto-crew, owned + switched on) and
    /// the headless bots. It only produces commands a player could tap, so the paid `dispatcher` product automates what a
    /// player can do by hand (IAP.md pay-to-win check; balance gate: within +-10 % of the hand-played bot).
    /// </summary>
    public static class Dispatcher
    {
        public static List<Cmd> Operate(Simulation sim, OperatorQ q)
        {
            var g = sim.Grid;
            var o = new List<Cmd>();
            if (g == null) return o;
            if (q.Crew) o.AddRange(CrewCommands(g));
            if (q.Setpoints) o.AddRange(SetpointCommands(g, q));
            if (q.Battery && g.PLevel[Grid.PBattery] > 0 && g.BatMode != 3) o.Add(Cmd.Battery("auto"));
            if (q.DemandResponse && g.DrLevel > 0 && g.Step >= g.DrCdUntil && g.NeedlePm < -40) o.Add(Cmd.DemandResponse());
            if (q.Shed) o.AddRange(ShedCommands(g));
            return o;
        }

        static long FaultWeight(Grid g, Fault f)
        {
            long w = 0;
            int s = Math.Min(g.Step, g.Steps - 1);
            for (int i = 0; i < g.DIds.Length; i++)
                if (g.DHooked[i] && (g.DCorr[i] == f.Corridor || i == f.District))
                    w += (long)g.Dem[i][s] * (f.Kind == "line" ? 1000 : f.Part) / 1000;
            return w * 1000 + 1000 - f.Id;
        }

        /// <summary>Nearest idle crew to the open faults, biggest dark load first.</summary>
        public static List<Cmd> CrewCommands(Grid g)
        {
            var o = new List<Cmd>();
            int idle = g.IdleCrews();
            if (idle <= 0) return o;
            var open = g.OpenFaults();
            var w = new Dictionary<int, long>();
            foreach (var f in open) w[f.Id] = FaultWeight(g, f);
            open.Sort((a, b) => w[b.Id].CompareTo(w[a.Id]));
            foreach (var f in open)
            {
                if (idle <= 0) break;
                if (f.Crew < 0)
                {
                    o.Add(Cmd.SendCrew(f.Id));
                    idle--;
                }
            }
            return o;
        }

        /// <summary>Merit order: renewables -> hydro inflow -> steam (cheap, slow: looks 10 s ahead) -> hydro peaking -> gas -> battery.</summary>
        public static List<Cmd> SetpointCommands(Grid g, OperatorQ q)
        {
            var o = new List<Cmd>();
            int s = g.Step;
            int taps = q.Taps;
            int bias = 0;
            if (g.PLevel[Grid.PBattery] > 0 && q.Battery)
            {
                // charge off-peak, shave the peak (the Auto battery absorbs / covers the bias)
                int peak = 0;
                for (int k = 0; k < g.Steps; k += 20) peak = Math.Max(peak, g.DemandAt(k));
                int nowD = g.DemandAt(s + 8);
                int pw = g.PCap[Grid.PBattery];
                if ((long)nowD * 100 < (long)peak * 78 && g.BatSoc * 10 < g.BatCapWh * 9) bias = pw * 6 / 10;
                else if ((long)nowD * 100 > (long)peak * 90 && g.BatSoc * 10 > g.BatCapWh * 2) bias = -pw * 7 / 10;
            }
            const int ls = 40, lf = 8;
            int inflow = 0;
            if (g.PLevel[Grid.PHydro] > 0)
            {
                inflow = g.InflowKw[Math.Min(s, g.Steps - 1)];
                if (g.ResWh * 10 > g.ResCapWh * 9) inflow = g.PCap[Grid.PHydro];
            }
            int needS = g.DemandAt(s + ls) - g.RenewablesAt(s + ls) - inflow + bias;
            int steamCap = g.PCap[Grid.PSteam];
            if (steamCap > 0)
            {
                int tgt = Math.Max(g.PMin[Grid.PSteam], Math.Min(needS, steamCap));
                o.AddRange(Taps(g, Grid.PSteam, StepsFor(g, Grid.PSteam, tgt), taps));
            }
            // fast units against the near-term residual
            int steamSoon = g.POut[Grid.PSteam];
            int stTarget = g.SetpointAfterPending(Grid.PSteam) * steamCap / 10;
            if (steamSoon < stTarget) steamSoon = Math.Min(stTarget, steamSoon + g.PRamp[Grid.PSteam] * lf);
            else if (steamSoon > stTarget) steamSoon = Math.Max(stTarget, steamSoon - g.PRamp[Grid.PSteam] * lf);
            int resid = g.DemandAt(s + lf) - g.RenewablesAt(s + lf) - steamSoon + bias;
            if (g.PLevel[Grid.PHydro] > 0)
            {
                int hcap = g.PCap[Grid.PHydro];
                long floorWh = g.ResCapWh / 10;
                int h = inflow;
                if (g.ResWh > floorWh) h = Math.Max(h, resid);
                h = (int)Math.Max(0, Math.Min(h, Math.Min(hcap, g.ResWh / Grid.WhPerKwStep)));
                o.AddRange(Taps(g, Grid.PHydro, StepsFor(g, Grid.PHydro, h), taps));
                resid -= h;
            }
            if (g.PLevel[Grid.PGas] > 0)
            {
                int gcap = g.PCap[Grid.PGas];
                if (resid > gcap / 10)
                {
                    if (g.POn[Grid.PGas] == 0) o.Add(Cmd.Power(true));
                    o.AddRange(Taps(g, Grid.PGas, StepsFor(g, Grid.PGas, Math.Min(resid, gcap)), taps));
                }
                else if (g.POn[Grid.PGas] == 1)
                {
                    // stop when nothing in the next 15 s needs it
                    int later = g.DemandAt(s + 60) - g.RenewablesAt(s + 60) - steamCap * 95 / 100 - (g.ResWh > g.ResCapWh / 5 ? g.PCap[Grid.PHydro] : 0) + bias;
                    if (later <= 0 && resid <= 0) o.Add(Cmd.Power(false));
                    else o.AddRange(Taps(g, Grid.PGas, 0, taps));
                }
            }
            return o;
        }

        static int StepsFor(Grid g, int p, int kw)
        {
            int cap = g.PCap[p];
            if (cap <= 0) return 0;
            int n = (int)Math.Round(kw * 10.0 / cap, MidpointRounding.AwayFromZero);
            return Math.Max(0, Math.Min(10, n));
        }

        static List<Cmd> Taps(Grid g, int p, int want, int taps)
        {
            int d = want - g.SetpointAfterPending(p);
            if (d == 0) return new List<Cmd>();
            return new List<Cmd> { Cmd.Ramp(GameData.Plants[p], d > 0 ? 1 : -1, Math.Min(Math.Abs(d), taps)) };
        }

        /// <summary>Manual shedding as a tool: when nothing is left to ramp, drop the lowest-priority district before the relays
        /// do; bring hand-shed districts back when the spinning reserve can carry them.</summary>
        public static List<Cmd> ShedCommands(Grid g)
        {
            var o = new List<Cmd>();
            long spare = 0;
            for (int p = 0; p <= Grid.PHydro; p++)
                if (g.PLevel[p] > 0 && g.PTrip[p] <= g.Step) spare += g.PCap[p] - g.POut[p];
            if (g.PLevel[Grid.PBattery] > 0) spare += Math.Min(g.PCap[Grid.PBattery], g.BatSoc / Grid.WhPerKwStep) - Math.Max(g.BatOut, 0);
            int s = Math.Min(g.Step, g.Steps - 1);
            if (g.NeedlePm < -50 && spare * 1000 < (long)g.LastDemand * 30)
            {
                int pick = -1;
                for (int i = 0; i < g.DIds.Length; i++)
                    if (g.IsLit(i) && i != g.Hospital && g.DState[i] == Grid.Lit && (pick < 0 || g.DPrio[i] < g.DPrio[pick])) pick = i;
                if (pick >= 0) o.Add(Cmd.Shed(g.DIds[pick]));
            }
            else if (g.NeedlePm > 30)
            {
                for (int i = 0; i < g.DIds.Length; i++)
                {
                    if (g.DState[i] == Grid.ManualShed && spare + (g.LastSupply - g.LastDemand) > (long)g.Dem[i][s] * 110 / 100)
                    {
                        o.Add(Cmd.Restore(g.DIds[i]));
                        break;
                    }
                }
            }
            return o;
        }

        /// <summary>Dispatcher assistant (owned + switched on in Settings): every 4 steps, the reference operator's commands.</summary>
        public static List<Cmd> AutoCommands(Simulation sim)
        {
            var s = sim.S;
            if (s.Auto.Count == 0) return new List<Cmd>();
            bool On(string part) => s.Auto.Contains(part) && (!s.AutoOn.TryGetValue(part, out bool v) || v);
            var q = new OperatorQ { Setpoints = On("baseload"), Battery = On("battery"), Crew = On("crew"), Shed = false, DemandResponse = false };
            if (!(q.Setpoints || q.Battery || q.Crew)) return new List<Cmd>();
            return Operate(sim, q);
        }

        static bool PendingOrRamping(Grid g, int p)
        {
            foreach (var pc in g.Pending) if (pc[1] == p) return true;
            int tgt = Math.Max(g.PMin[p], g.PSteps[p] * g.PCap[p] / 10);
            return Math.Abs(g.POut[p] - tgt) > g.PRamp[p] * 4;
        }

        /// <summary>Naive player: taps only what the coach highlights / the obvious peak action (balance gate: >= 2 stars in
        /// >= 80 % of chapter-1 shifts). The coach shows + while the needle is low or a district waits in the dark, - while it
        /// is high, never while a change on that plant is still on its way ("takes effect in N s").</summary>
        public static List<Cmd> Naive(Simulation sim)
        {
            var g = sim.Grid;
            var o = new List<Cmd>();
            if (g == null) return o;
            foreach (var f in g.OpenFaults())
            {
                if (f.Crew < 0 && g.Step - f.Step >= 12 && g.IdleCrews() > 0)
                {
                    o.Add(Cmd.SendCrew(f.Id));
                    break;
                }
            }
            bool dark = false;
            for (int i = 0; i < g.DIds.Length; i++) if (g.DHooked[i] && g.DState[i] == Grid.AutoShed) dark = true;
            if (g.NeedlePm < -30 || (dark && g.NeedlePm < 30))
            {
                if (g.SetpointAfterPending(Grid.PSteam) < 10 && !PendingOrRamping(g, Grid.PSteam)) o.Add(Cmd.Ramp("steam", 1));
                else if (g.PLevel[Grid.PGas] > 0)
                {
                    if (g.POn[Grid.PGas] == 0) o.Add(Cmd.Power(true));
                    else if (!PendingOrRamping(g, Grid.PGas)) o.Add(Cmd.Ramp("gas", 1));
                }
            }
            else if (g.NeedlePm > 30 && !dark)
            {
                if (g.POn[Grid.PGas] == 1 && g.SetpointAfterPending(Grid.PGas) > 0)
                {
                    if (!PendingOrRamping(g, Grid.PGas)) o.Add(Cmd.Ramp("gas", -1));
                }
                else if (g.POn[Grid.PGas] == 1) o.Add(Cmd.Power(false));
                else if (g.SetpointAfterPending(Grid.PSteam) > 0 && !PendingOrRamping(g, Grid.PSteam)) o.Add(Cmd.Ramp("steam", -1));
            }
            return o;
        }
    }
}
