// Volt City sim core — engine independent (no UnityEngine). C# 9 / .NET Standard 2.1.
using System;
using System.Collections.Generic;

namespace VoltCity.Sim
{
    /// <summary>Per-shift counters (all integers).</summary>
    public sealed class ShiftAcc
    {
        public long DemWh, ServedWh, WastedWh, FestWh, BackupWh, BandSteps, Shed, AutoShed, Trips, LineTrips, Faults, Repairs, Jobs, Dr, Imports,
            TestDark, DeficitSteps, SurplusSteps, Restores, BackupRuns;

        static readonly string[] Keys = { "dem_wh", "served_wh", "wasted_wh", "fest_wh", "backup_wh", "band_steps", "shed", "auto_shed", "trips", "line_trips",
            "faults", "repairs", "jobs", "dr", "imports", "test_dark", "deficit_steps", "surplus_steps", "restores", "backup_runs" };

        long[] Values() => new[] { DemWh, ServedWh, WastedWh, FestWh, BackupWh, BandSteps, Shed, AutoShed, Trips, LineTrips, Faults, Repairs, Jobs, Dr, Imports,
            TestDark, DeficitSteps, SurplusSteps, Restores, BackupRuns };

        public JObj ToJson()
        {
            var o = new JObj();
            var v = Values();
            for (int i = 0; i < Keys.Length; i++) o[Keys[i]] = v[i];
            return o;
        }

        public static ShiftAcc FromJson(object o)
        {
            long G(string k) => J.L(o, k);
            return new ShiftAcc
            {
                DemWh = G("dem_wh"), ServedWh = G("served_wh"), WastedWh = G("wasted_wh"), FestWh = G("fest_wh"), BackupWh = G("backup_wh"), BandSteps = G("band_steps"),
                Shed = G("shed"), AutoShed = G("auto_shed"), Trips = G("trips"), LineTrips = G("line_trips"), Faults = G("faults"), Repairs = G("repairs"), Jobs = G("jobs"),
                Dr = G("dr"), Imports = G("imports"), TestDark = G("test_dark"), DeficitSteps = G("deficit_steps"), SurplusSteps = G("surplus_steps"),
                Restores = G("restores"), BackupRuns = G("backup_runs")
            };
        }
    }

    /// <summary>
    /// One live shift of the power grid (DESIGN section 3 sim model). Fixed step 0.25 s sim time (960 steps = 24 diorama
    /// hours), integer state only (kW, Wh, steps, ppm) so a snapshot survives JSON exactly (LL Pit Wall P1). Doubles are
    /// used inside a step only.
    ///
    /// Per step: due setpoints -> planned faults / trips / event marks -> district demand (precomputed profile x growth x
    /// weather x events, minus Demand Response) -> plant outputs (ramp, min, trips, gas start, hydro reservoir, wind / solar
    /// availability) -> battery mode -> governor (primary response of running thermal / hydro units) -> curtailment ->
    /// balance needle -> brownout / auto-shedding (B &lt; -5 % for 10 s, lowest priority first, Hospital Hill last after its
    /// 60 s backup) / over-supply trip (B &gt; +8 % for 20 s) -> auto-restore -> line flows + overheat + trip + reroute
    /// (smart substations) -> crews (road-graph travel, repair) -> storage -> counters.
    /// TutorialSafe: no auto-shedding, no trips, no overheat trips, no planned bad events (LL Pit Lane Boss).
    /// </summary>
    public sealed class Grid
    {
        public const int PSteam = 0, PGas = 1, PHydro = 2, PWind = 3, PSolar = 4, PBattery = 5;
        public static readonly string[] Modes = { "charge", "hold", "discharge", "auto" };
        public const int WhPerKwStep = 25;            // 1 kW for 0.25 s sim = 1 kW for 1.5 diorama minutes = 25 Wh
        public const int HeatFull = 1000000;
        public const int Lit = 0, AutoShed = 1, ManualShed = 2, Backup = 3;

        public GameData Gd;
        public ShiftPlan Plan;
        public int Steps = 960;
        public int Step;
        public bool TutorialSafe;
        public readonly List<SimEvent> Events = new List<SimEvent>();

        // ---- static per shift (rebuilt from plan + context on load)
        // DRoute is the last completed step's route cache; persist it because controls query it between steps.
        public string[] DIds;
        public bool[] DHooked;
        public int[] DPrio, DCorr, DGrowth, DRoute;
        int[] DByPrio;
        public string[] DSite;
        public int[][] Dem;                            // kW per district per step
        public string[] CIds, CSite;
        public int[] CCap, CLevel, CTie;
        public int[] PLevel, PCap, PMin, PRamp, PDelay;
        public bool[] PGov;
        public double[] PFuel;
        public double[] WindCf, SolarCf;
        public int[] InflowKw;
        public long BatCapWh, ResCapWh;
        public int BatEff = 95, HilltopCorr = -1, Hospital = -1, Market = -1;
        public int FestFrom = -1, FestTo = -1, FestPct, TestFrom = -1, TestTo = -1, GasSpikePct;
        public int CrewsN = 1, CrewSpeedPct = 100, DrLevel;
        readonly List<(int step, string kind, string id)> marks = new List<(int, string, string)>();

        // ---- dynamic state (integers; ToJson / RestoreFrom)
        public int[] DState, DPart, DBackupUntil, DDarkSteps;
        public long[] DDemWh, DSrvWh;
        /// <summary>Cold-load pickup after a repair (owner #183-2c): start step (-1 = none), the per mille of the district coming
        /// back and its progress (steps); it advances only while the supply carries it, over ~10 s.</summary>
        public int[] DPickAt, DPickPart, DPickProg;
        public const int PickupSteps = 40, PickupStartPm = 300;
        /// <summary>Per mille of district i's load still held back by the cold-load pickup at the current step.</summary>
        public int PickOff(int i)
        {
            if (DPickAt == null || DPickAt[i] < 0) return 0;
            int e = DPickProg[i];
            if (e >= PickupSteps) return 0;
            int ramp = PickupStartPm + (1000 - PickupStartPm) * Math.Max(0, e) / PickupSteps;
            return DPickPart[i] * (1000 - ramp) / 1000;
        }
        bool AnyPickup()
        {
            for (int i = 0; i < DPickAt.Length; i++) if (PickOff(i) > 0) return true;
            return false;
        }
        void StartPickup(int i, int part)
        {
            if (part <= 0) return;
            DPickPart[i] = Math.Min(1000, PickOff(i) + part);
            DPickAt[i] = Step;
            DPickProg[i] = 0;
        }
        public int[] PSteps, POut, POn, PReady, PTrip;
        public long[] PGenWh;
        public List<int[]> Pending = new List<int[]>();     // [apply step, plant, delta]
        public long BatSoc, ResWh;
        public int BatMode = 3, BatOut;
        public int[] CHeat, COutUntil, CFault, CFlow;
        public List<Fault> Faults = new List<Fault>();
        public List<CrewTruck> Crews = new List<CrewTruck>();
        public int NextFault = 1, FaultPtr, MarkPtr, DeficitSteps, SurplusSteps, RestoreSteps;
        public int DrUntil = -1, DrCdUntil = -1, ImportUntil = -1, ImportKw;
        public long RngState = 1;
        public int NeedlePm, LastDemand, LastSupply;
        public ShiftAcc Acc = new ShiftAcc();
        public List<int[]> Hourly = new List<int[]>();     // [demand kW, supply kW, needle pm, lit mask] per diorama hour

        // ---- scratch (no allocation per step)
        int[] sReq, sOn, sRt, sUp, sDn, sGen, sFlows;

        // ------------------------------------------------------------------------------------------- setup

        public void Setup(GameData gd, ShiftPlan plan, ShiftContext ctx)
        {
            Gd = gd;
            Plan = plan;
            Static(ctx);
            int nd = DIds.Length, np = 6, nc = CIds.Length;
            DState = new int[nd]; DPart = new int[nd]; DBackupUntil = Fill(nd, -1); DDarkSteps = new int[nd]; DDemWh = new long[nd]; DSrvWh = new long[nd]; DPickAt = Fill(nd, -1); DPickPart = new int[nd]; DPickProg = new int[nd];
            PSteps = new int[np]; POut = new int[np]; POn = new int[np]; PReady = new int[np]; PTrip = Fill(np, -1); PGenWh = new long[np];
            var carry = ctx.Carry ?? new Carry();
            for (int i = 0; i < np; i++)
            {
                PSteps[i] = Clamp(carry.Steps.TryGetValue(GameData.Plants[i], out int v) ? v : 0, 0, 10);
                POn[i] = PLevel[i] > 0 ? 1 : 0;
                if (i == PGas) POn[i] = PLevel[i] > 0 && carry.GasOn == 1 ? 1 : 0;
                if (PLevel[i] > 0 && i <= PHydro && POn[i] == 1) POut[i] = Math.Max(PMin[i], PSteps[i] * PCap[i] / 10);
            }
            BatSoc = ClampL(carry.BatSoc, 0, BatCapWh);
            BatMode = Clamp(carry.BatMode, 0, 3);
            ResWh = ClampL(carry.ResWh, 0, ResCapWh);
            CHeat = new int[nc]; COutUntil = Fill(nc, -1); CFault = Fill(nc, -1); CFlow = new int[nc];
            Crews = new List<CrewTruck>();
            for (int i = 0; i < CrewsN; i++) Crews.Add(new CrewTruck { Id = i, Node = gd.Depot, State = CrewTruck.Idle });
            Acc = new ShiftAcc();
            RngState = Rng.PosMod(plan.Seed * 7 + 3, 2147483646) + 1;
            Routes(0);
        }

        static int[] Fill(int n, int v) { var a = new int[n]; for (int i = 0; i < n; i++) a[i] = v; return a; }
        static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);
        static long ClampL(long v, long lo, long hi) => v < lo ? lo : (v > hi ? hi : v);

        void Static(ShiftContext ctx)
        {
            var gd = Gd;
            Steps = gd.Steps;
            int nc = gd.Corridors.Count;
            CIds = new string[nc]; CSite = new string[nc]; CCap = new int[nc]; CLevel = new int[nc]; CTie = Fill(nc, -1);
            for (int c = 0; c < nc; c++)
            {
                var cd = gd.Corridors[c];
                CIds[c] = cd.Id;
                CSite[c] = cd.Site;
                int lv = Clamp(ctx.Lines.TryGetValue(cd.Id, out int l) ? l : 0, 0, 2);
                CLevel[c] = lv;
                CCap[c] = (int)(cd.CapacityMw[lv] * 1000.0);
            }
            foreach (var tid in ctx.Ties)
            {
                if (!gd.Tie.TryGetValue(tid, out var t)) continue;
                int a = Array.IndexOf(CIds, t.A), b = Array.IndexOf(CIds, t.B);
                if (a < 0 || b < 0) continue;
                CTie[a] = b;
                CTie[b] = a;
            }
            int nd = gd.Districts.Count;
            DIds = new string[nd]; DSite = new string[nd]; DHooked = new bool[nd]; DPrio = new int[nd]; DCorr = new int[nd]; DGrowth = new int[nd];
            for (int i = 0; i < nd; i++)
            {
                var d = gd.Districts[i];
                DIds[i] = d.Id;
                DSite[i] = d.Site;
                DHooked[i] = ctx.HookedGrowthPm.ContainsKey(d.Id);
                DPrio[i] = d.Priority;
                DCorr[i] = Array.IndexOf(CIds, d.Corridor);
                DGrowth[i] = DHooked[i] ? Clamp(ctx.HookedGrowthPm[d.Id], 0, gd.GrowthMaxPm) : 0;
            }
            DRoute = Fill(nd, -1);
            var byPrio = new List<int>();
            for (int i = 0; i < nd; i++) byPrio.Add(i);
            byPrio.Sort((a, z) => DPrio[a] != DPrio[z] ? DPrio[z].CompareTo(DPrio[a]) : a.CompareTo(z));
            DByPrio = byPrio.ToArray();
            HilltopCorr = Array.IndexOf(CIds, "c_hilltop");
            Hospital = Array.IndexOf(DIds, "hospital_hill");
            Market = Array.IndexOf(DIds, "market_square");
            PLevel = new int[6]; PCap = new int[6]; PMin = new int[6]; PRamp = new int[6]; PDelay = new int[6]; PGov = new bool[6]; PFuel = new double[6];
            for (int i = 0; i < 6; i++)
            {
                var p = gd.PlantDefs[i];
                int lv = Clamp(ctx.Plants.TryGetValue(p.Id, out int l) ? l : 0, 0, 3);
                PLevel[i] = lv;
                int cap = lv > 0 ? (int)(p.CapacityMw[lv - 1] * 1000.0) : 0;
                PCap[i] = cap;
                PMin[i] = cap * p.MinPct / 100;
                PRamp[i] = Math.Max(1, (int)(cap * p.RampPctS / 100.0 / 4.0));
                PDelay[i] = (int)Math.Round(p.DelayS * 4.0, MidpointRounding.AwayFromZero);
                PGov[i] = p.Governor;
                PFuel[i] = p.Fuel;
            }
            var bp = gd.Plant["battery"];
            BatCapWh = PLevel[PBattery] > 0 ? (long)(bp.EnergyMwh[PLevel[PBattery] - 1] * 1e6) : 0;
            BatEff = bp.EffPct;
            var hp = gd.Plant["hydro"];
            ResCapWh = PLevel[PHydro] > 0 ? (long)(hp.ReservoirMwh[PLevel[PHydro] - 1] * 1e6) : 0;
            CrewsN = Clamp(ctx.Crews, 1, 5);
            int csl = Clamp(ctx.CrewSpeed, 0, gd.CrewSpeedUpgradesPct.Length);
            CrewSpeedPct = csl == 0 ? 100 : gd.CrewSpeedUpgradesPct[csl - 1];
            DrLevel = Clamp(ctx.Dr, 0, 2);
            sReq = new int[nd]; sOn = new int[nd]; sRt = new int[nd]; sUp = new int[3]; sDn = new int[3]; sGen = new int[6]; sFlows = new int[nc];
            Curves();
        }

        /// <summary>Demand per district per step, wind / solar availability, hydro inflow, event marks (pure function of the plan).</summary>
        void Curves()
        {
            var gd = Gd;
            int sph = gd.StepsPerHour;
            double startH = Plan.StartMin / 60.0;
            var wx = gd.WeatherById.TryGetValue(Plan.Weather, out var w0) ? w0 : gd.Weather[0];
            var temp = gd.Temp.TryGetValue(wx.Temp, out var t0) ? t0 : new TempDef();
            double noise = gd.NoisePct / 100.0;
            long seed = Plan.Seed;
            var cityMult = new double[Steps];
            var windMult = new double[Steps];
            var solarMult = new double[Steps];
            var storm = new bool[Steps];
            for (int s = 0; s < Steps; s++) { cityMult[s] = 1; windMult[s] = 1; solarMult[s] = 1; }
            var distMult = new double[DIds.Length][];
            for (int i = 0; i < DIds.Length; i++) { distMult[i] = new double[Steps]; for (int s = 0; s < Steps; s++) distMult[i][s] = 1; }
            FestFrom = -1; FestTo = -1; FestPct = 0; TestFrom = -1; TestTo = -1; GasSpikePct = 0;
            marks.Clear();
            foreach (var e in Plan.Events)
            {
                int a = Clamp(e.From, 0, Steps), z = Clamp(e.To, 0, Steps);
                if (z < a) z = Steps;
                marks.Add((a, "start", e.Id));
                marks.Add((Math.Max(z, a), "end", e.Id));
                switch (e.Id)
                {
                    case "heatwave": case "cold_snap": case "halftime_surge":
                        for (int s = a; s < z; s++) cityMult[s] *= 1.0 + e.Pct / 100.0;
                        break;
                    case "match_night": case "new_district_rush":
                    {
                        int di = Array.IndexOf(DIds, e.District);
                        if (di >= 0) for (int s = a; s < z; s++) distMult[di][s] *= 1.0 + e.Pct / 100.0;
                        break;
                    }
                    case "festival":
                        if (Market >= 0) for (int s = a; s < z; s++) distMult[Market][s] *= 1.0 + e.Pct / 100.0;
                        FestFrom = a; FestTo = z; FestPct = e.TariffPct;
                        break;
                    case "hospital_test":
                        TestFrom = a; TestTo = z;
                        break;
                    case "fog":
                        for (int s = a; s < z; s++) windMult[s] *= 1.0 + e.WindPct / 100.0;
                        break;
                    case "cloud_front": case "eclipse":
                        for (int s = a; s < z; s++) solarMult[s] *= 1.0 + e.SolarPct / 100.0;
                        break;
                    case "thunderstorm":
                        for (int s = a; s < z; s++) storm[s] = true;
                        break;
                    case "fuel_spike":
                        GasSpikePct = e.GasPct;
                        break;
                }
            }
            // stable order: by step, "start" before "end" at the same step, then insertion
            var indexed = new List<KeyValuePair<int, (int step, string kind, string id)>>();
            for (int i = 0; i < marks.Count; i++) indexed.Add(new KeyValuePair<int, (int, string, string)>(i, marks[i]));
            indexed.Sort((x, y) =>
            {
                if (x.Value.step != y.Value.step) return x.Value.step.CompareTo(y.Value.step);
                int kx = x.Value.kind == "start" ? 0 : 1, ky = y.Value.kind == "start" ? 0 : 1;
                return kx != ky ? kx.CompareTo(ky) : x.Key.CompareTo(y.Key);
            });
            marks.Clear();
            foreach (var kv in indexed) marks.Add(kv.Value);
            int hours = Steps / sph + 2;
            Dem = new int[DIds.Length][];
            for (int i = 0; i < DIds.Length; i++)
            {
                var arr = new int[Steps];
                Dem[i] = arr;
                if (!DHooked[i]) continue;
                var d = gd.District[DIds[i]];
                var prof = gd.Profiles[d.Profile];
                double peak = d.PeakMw * 1000.0 * (1.0 + DGrowth[i] / 1000.0);
                var nz = new double[hours];
                var r = new Rng(Rng.Mix(seed, 31 + i));
                for (int h = 0; h < hours; h++) nz[h] = (r.Frand() * 2.0 - 1.0) * noise;
                var dm = distMult[i];
                for (int s = 0; s < Steps; s++)
                {
                    double hf = startH + (double)s / sph;
                    double hod = hf % 24.0;
                    int h0 = (int)hod;
                    double f = hod - h0;
                    double pv = prof[h0] * (1.0 - f) + prof[h0 + 1] * f;
                    int k = s / sph;
                    double fk = (double)(s % sph) / sph;
                    double nv = nz[k] * (1.0 - fk) + nz[k + 1] * fk;
                    double tv = 1.0;
                    if (temp.Pct != 0 && hod >= temp.FromH && hod < temp.ToH) tv += temp.Pct / 100.0;
                    arr[s] = (int)(peak * pv * (1.0 + nv) * tv * cityMult[s] * dm[s]);
                }
            }
            WindCf = new double[Steps];
            SolarCf = new double[Steps];
            InflowKw = new int[Steps];
            var rw = new Rng(Rng.Mix(seed, 7));
            var wn = new double[hours];
            var cl = new double[hours];
            for (int h = 0; h < hours; h++)
            {
                wn[h] = (rw.Frand() * 2.0 - 1.0) * 0.12;
                cl[h] = 0.85 + rw.Frand() * 0.15;
            }
            double rise = gd.SolarRiseH, sset = gd.SolarSetH;
            var hp = gd.Plant["hydro"];
            double inflowBase = PLevel[PHydro] > 0 ? hp.InflowMw[PLevel[PHydro] - 1] * 1000.0 : 0.0;
            for (int s = 0; s < Steps; s++)
            {
                double hod = (startH + (double)s / sph) % 24.0;
                int k = s / sph;
                double fk = (double)(s % sph) / sph;
                double wv = wx.Wind + wx.Swing * Math.Sin((hod - 9.0) / 24.0 * 2.0 * Math.PI) + (wn[k] * (1.0 - fk) + wn[k + 1] * fk);
                double sol = 0.0;
                if (hod > rise && hod < sset) sol = Math.Sin(Math.PI * (hod - rise) / (sset - rise)) * wx.Solar * (cl[k] * (1.0 - fk) + cl[k + 1] * fk);
                double inflow = inflowBase * wx.Inflow;
                if (storm[s])
                {
                    wv = gd.StormWind;
                    double clear = Math.Sin(Math.PI * Math.Max(0.0, Math.Min(1.0, (hod - rise) / (sset - rise))));
                    sol = Math.Min(sol, clear * gd.StormSolar);
                    inflow = inflowBase * gd.StormInflow;
                }
                WindCf[s] = Math.Max(0.0, Math.Min(1.0, wv * windMult[s]));
                SolarCf[s] = Math.Max(0.0, Math.Min(1.0, sol * solarMult[s]));
                InflowKw[s] = (int)inflow;
            }
        }

        // ------------------------------------------------------------------------------------------- commands

        SimEvent Emit(string type)
        {
            var e = new SimEvent(type, Step);
            Events.Add(e);
            return e;
        }

        /// <summary>Applied at the start of a fixed step (Simulation queues taps until then).</summary>
        public bool Apply(Cmd cmd)
        {
            if (cmd == null) return false;
            switch (cmd.Type)
            {
                case "ramp":
                {
                    int p = Gd.PlantIndex(cmd.Plant);
                    if (p < 0 || p > PHydro || PLevel[p] == 0) return false;
                    int dlt = Clamp(cmd.Dir, -1, 1) * Clamp(cmd.N, 1, 10);
                    if (dlt == 0) return false;
                    Pending.Add(new[] { Step + PDelay[p], p, dlt });
                    return true;
                }
                case "power":
                    if (PLevel[PGas] == 0) return false;
                    if (cmd.On && POn[PGas] == 0)
                    {
                        POn[PGas] = 1;
                        PReady[PGas] = Step + (int)Math.Round(Gd.Plant["gas"].StartS * 4.0, MidpointRounding.AwayFromZero);
                    }
                    else if (!cmd.On) POn[PGas] = 0;
                    return true;
                case "battery":
                {
                    int m = Array.IndexOf(Modes, cmd.Mode);
                    if (m < 0 || PLevel[PBattery] == 0) return false;
                    BatMode = m;
                    return true;
                }
                case "crew":
                    return Dispatch(cmd.Fault, cmd.Crew);
                case "shed":
                {
                    int i = Array.IndexOf(DIds, cmd.District);
                    if (i < 0 || !DHooked[i] || DState[i] != Lit || i == Hospital) return false;
                    DState[i] = ManualShed;
                    Acc.Shed++;
                    Emit("shed").With("district", DIds[i]).With("auto", false);
                    return true;
                }
                case "restore":
                {
                    int i = Array.IndexOf(DIds, cmd.District);
                    if (i < 0 || (DState[i] != AutoShed && DState[i] != ManualShed)) return false;
                    DState[i] = Lit;
                    Acc.Restores++;
                    Emit("restore").With("district", DIds[i]).With("auto", false);
                    return true;
                }
                case "dr":
                    if (DrLevel == 0 || Step < DrCdUntil) return false;
                    DrUntil = Step + Gd.DrDurS * 4;
                    DrCdUntil = Step + (Gd.DrDurS + Gd.DrCooldownS) * 4;
                    Acc.Dr++;
                    Emit("dr").With("until", DrUntil);
                    return true;
                case "import":
                    ImportKw = Math.Max(1000, LastDemand * Gd.ImportPct / 100);
                    ImportUntil = Step + Gd.ImportDurS * 4;
                    Acc.Imports++;
                    Emit("import").With("kw", ImportKw).With("until", ImportUntil);
                    return true;
                case "overtime":
                {
                    var f = FindFault(cmd.Fault);
                    if (f == null || !f.Open) return false;
                    RepairFault(f, true);
                    return true;
                }
                case "script_fault":
                {
                    if (!TutorialSafe) return false;
                    int ci = Array.IndexOf(CIds, cmd.Corridor);
                    if (ci < 0 || CFault[ci] >= 0) return false;
                    AddFault(new PlanFault { Kind = "line", Corridor = CIds[ci], RepairS = Gd.LineRepairS, Cause = "tutorial" });
                    return true;
                }
            }
            return false;
        }

        public Fault FindFault(int id)
        {
            foreach (var f in Faults) if (f.Id == id) return f;
            return null;
        }

        public List<Fault> OpenFaults()
        {
            var o = new List<Fault>();
            foreach (var f in Faults) if (f.Open) o.Add(f);
            return o;
        }

        public int IdleCrews()
        {
            int n = 0;
            foreach (var c in Crews) if (c.State == CrewTruck.Idle || c.State == CrewTruck.Return) n++;
            return n;
        }

        public int TravelSteps(string from, string to)
        {
            int dist = Gd.RoadDistance(from, to);
            double mps = Gd.CrewSpeedMps * CrewSpeedPct / 100.0;
            return Math.Max(1, (int)Math.Ceiling(dist / mps * 4.0));
        }

        bool Dispatch(int faultId, int crewId)
        {
            var f = FindFault(faultId);
            if (f == null || !f.Open || f.Crew >= 0) return false;
            int best = -1, bestT = int.MaxValue;
            foreach (var c in Crews)
            {
                if (c.State != CrewTruck.Idle && c.State != CrewTruck.Return) continue;
                if (crewId >= 0 && c.Id != crewId) continue;
                int t = TravelSteps(c.Node, f.Site);
                if (t < bestT) { bestT = t; best = c.Id; }
            }
            if (best < 0) return false;
            var cr = Crews[best];
            string from = cr.Node;
            cr.State = CrewTruck.Travel;
            cr.Until = Step + bestT;
            cr.Job = f.Id;
            f.Crew = best;
            Acc.Jobs++;
            Emit("crew_sent").With("crew", best).With("fault", f.Id).With("eta", cr.Until).With("from", from).With("to", f.Site);
            return true;
        }

        void AddFault(PlanFault spec)
        {
            var f = new Fault { Id = NextFault, Kind = spec.Kind, Step = Step, Repair = Math.Max(1, spec.RepairS * 4), Cause = spec.Cause };
            if (spec.Kind == "line")
            {
                int ci = Array.IndexOf(CIds, spec.Corridor);
                if (ci < 0 || CFault[ci] >= 0) return;        // never two faults on one corridor
                f.Corridor = ci;
                f.Site = CSite[ci];
                CFault[ci] = NextFault;
            }
            else
            {
                int di = Array.IndexOf(DIds, spec.District);
                if (di < 0 || !DHooked[di]) return;
                f.District = di;
                f.Site = DSite[di];
                f.Part = spec.PartPm > 0 ? spec.PartPm : 250;
                DPart[di] = Math.Min(1000, DPart[di] + f.Part);
            }
            NextFault++;
            Faults.Add(f);
            Acc.Faults++;
            Emit("fault").With("fault", f.Id).With("kind", f.Kind).With("site", f.Site).With("cause", f.Cause)
                .With("corridor", f.Corridor >= 0 ? CIds[f.Corridor] : "").With("district", f.District >= 0 ? DIds[f.District] : "");
        }

        void RepairFault(Fault f, bool instant)
        {
            f.Done = Step;
            if (f.Corridor >= 0)
            {
                for (int i = 0; i < DIds.Length; i++)
                    if (DHooked[i] && DState[i] == Lit && DRoute[i] < 0 && DCorr[i] == f.Corridor) StartPickup(i, 1000 - DPart[i]);
                CFault[f.Corridor] = -1; CHeat[f.Corridor] = 0;
            }
            if (f.District >= 0)
            {
                int was = DPart[f.District];
                DPart[f.District] = Math.Max(0, was - f.Part);
                if (DState[f.District] == Lit) StartPickup(f.District, was - DPart[f.District]);
            }
            if (f.Crew >= 0)
            {
                var cr = Crews[f.Crew];
                cr.State = CrewTruck.Idle;
                cr.Node = f.Site;
                cr.Until = Step + Gd.CrewReturnAfterS * 4;
                cr.Job = -1;
            }
            Acc.Repairs++;
            Emit("repaired").With("fault", f.Id).With("instant", instant).With("site", f.Site).With("district", FaultDistrict(f));
        }

        public string FaultDistrict(Fault f)
        {
            if (f.District >= 0) return DIds[f.District];
            for (int i = 0; i < DIds.Length; i++) if (DCorr[i] == f.Corridor) return DIds[i];
            return "";
        }

        // ------------------------------------------------------------------------------------------- step

        public bool Finished => Step >= Steps;

        double Rand()
        {
            RngState = (RngState * 48271L) % 2147483647L;
            return (double)(RngState - 1) / 2147483646.0;
        }

        bool CorridorUp(int c) => c >= 0 && CFault[c] < 0 && COutUntil[c] <= Step;

        /// <summary>The corridor feeding district i this step (-1 = no path = dark). Derived each step by Routes().</summary>
        public int Route(int i) => DRoute[i];

        bool AnyOpenLineFault()
        {
            foreach (var f in Faults) if (f.Open && f.Corridor >= 0) return true;
            return false;
        }

        /// <summary>The part (per mille) of district i that is dark through open transformer faults a crew is already on.</summary>
        int DispatchedPart(int i)
        {
            int part = 0;
            foreach (var f in Faults) if (f.Open && f.District == i && f.Crew >= 0) part += f.Part;
            return Math.Min(1000, part);
        }

        /// <summary>Own corridor if in service, else the smart-substation partner only when the partner has room for the
        /// district (a reroute never overloads the healthy line into a cascade). Highest priority reroutes first.</summary>
        void Routes(int s)
        {
            s = Clamp(s, 0, Steps - 1);
            var load = new int[CIds.Length];
            for (int i = 0; i < DIds.Length; i++)
            {
                DRoute[i] = -1;
                if (DHooked[i] && CorridorUp(DCorr[i]))
                {
                    DRoute[i] = DCorr[i];
                    if (DState[i] == Lit) load[DCorr[i]] += Dem[i][s] * (1000 - DPart[i]) / 1000;
                }
            }
            foreach (int i in DByPrio)
            {
                if (!DHooked[i] || DRoute[i] >= 0) continue;
                int t = CTie[DCorr[i]];
                int need = Dem[i][s] * (1000 - DPart[i]) / 1000;
                if (CorridorUp(t) && load[t] + need <= CCap[t])
                {
                    DRoute[i] = t;
                    if (DState[i] == Lit) load[t] += need;
                }
            }
        }

        int BatDischargeMax() => (int)Math.Min(PCap[PBattery], (long)((double)BatSoc / WhPerKwStep * BatEff / 100.0));
        int BatChargeMax() => (int)Math.Min(PCap[PBattery], (long)((double)(BatCapWh - BatSoc) / WhPerKwStep * 100.0 / BatEff));

        public void AdvanceStep()
        {
            if (Step >= Steps) return;
            int s = Step;
            int nd = DIds.Length;
            // due setpoint changes
            if (Pending.Count > 0)
            {
                var keep = new List<int[]>();
                foreach (var pc in Pending)
                {
                    if (pc[0] <= s) PSteps[pc[1]] = Clamp(PSteps[pc[1]] + pc[2], 0, 10);
                    else keep.Add(pc);
                }
                Pending = keep;
            }
            // planned faults / trips / marks
            while (FaultPtr < Plan.Faults.Count && Plan.Faults[FaultPtr].Step <= s)
            {
                if (!TutorialSafe) AddFault(Plan.Faults[FaultPtr]);
                FaultPtr++;
            }
            foreach (var tr in Plan.Trips)
            {
                if (tr.Step != s || TutorialSafe) continue;
                int p = Gd.PlantIndex(tr.Plant);
                if (p >= 0 && PLevel[p] > 0 && POut[p] > 0) TripPlant(p, tr.Dur, "event");
            }
            while (MarkPtr < marks.Count && marks[MarkPtr].step <= s)
            {
                Emit("event_" + marks[MarkPtr].kind).With("id", marks[MarkPtr].id);
                MarkPtr++;
            }
            Routes(s);
            // demand
            double drF = 1.0;
            if (s < DrUntil) drF = 1.0 - Gd.DrPct[Math.Max(DrLevel, 1) - 1] / 100.0;
            int demand = 0, reqTotal = 0, backupKw = 0, excusedKw = 0, heldKw = 0;
            // fair third star (owner #183-2b): a fault's dark time counts against the shift only until a crew is dispatched.
            // Line faults are excused when every open line fault has a crew; transformer faults per district by their part.
            bool linesDispatched = true;
            foreach (var f in Faults) if (f.Open && f.Corridor >= 0 && f.Crew < 0) { linesDispatched = false; break; }
            for (int i = 0; i < nd; i++)
            {
                sRt[i] = -1; sReq[i] = 0; sOn[i] = 0;
                if (!DHooked[i]) continue;
                int r = (int)(Dem[i][s] * drF);
                sReq[i] = r;
                reqTotal += r;
                if (DState[i] == Backup) { backupKw += r * (1000 - DPart[i]) / 1000; continue; }
                if (DState[i] != Lit) continue;
                int rt = Route(i);
                sRt[i] = rt;
                if (rt < 0) { if (linesDispatched && AnyOpenLineFault()) excusedKw += r; continue; }
                if (DPart[i] > 0) excusedKw += r * Math.Min(DPart[i], DispatchedPart(i)) / 1000;
                int on = r * (1000 - DPart[i]) / 1000;
                int hold = r * Math.Min(1000 - DPart[i], PickOff(i)) / 1000;   // cold-load pickup: returning load comes back over ~10 s
                on -= hold;
                if (hold > 0 && Step - DPickAt[i] < PickupSteps) excusedKw += hold;   // past ~10 s a held-back share counts as unserved
                else heldKw += hold;                                                   // ... and shows on the needle
                sOn[i] = on;
                demand += on;
            }
            // plant outputs (ramp toward setpoint)
            bool windPath = HilltopCorr >= 0 && (CorridorUp(HilltopCorr) || (CTie[HilltopCorr] >= 0 && CorridorUp(CTie[HilltopCorr])));
            for (int p = 0; p <= PHydro; p++)
            {
                if (PLevel[p] == 0) continue;
                if (PTrip[p] > s || POn[p] == 0 || (p == PGas && PReady[p] > s)) { POut[p] = 0; continue; }
                int target = Math.Max(PMin[p], PSteps[p] * PCap[p] / 10);
                if (p == PHydro) target = (int)Math.Min(target, ResWh / WhPerKwStep);
                int o = POut[p];
                if (o < target) o = Math.Min(target, o + PRamp[p]);
                else if (o > target) o = Math.Max(target, o - PRamp[p]);
                POut[p] = o;
            }
            int windAv = PLevel[PWind] > 0 && windPath ? (int)(PCap[PWind] * WindCf[s]) : 0;
            int solarAv = PLevel[PSolar] > 0 ? (int)(PCap[PSolar] * SolarCf[s]) : 0;
            int availRen = windAv + solarAv;
            int thermal = POut[PSteam] + POut[PGas] + POut[PHydro];
            int imp = s < ImportUntil ? ImportKw : 0;
            // battery (manual modes first)
            int bat = 0;
            int batPow = PCap[PBattery];
            if (PLevel[PBattery] > 0)
            {
                if (BatMode == 0) bat = -BatChargeMax();
                else if (BatMode == 2) bat = BatDischargeMax();
            }
            int residual = demand - (thermal + availRen + imp + bat);
            if (PLevel[PBattery] > 0 && BatMode == 3)
            {
                if (residual > 0) bat = Math.Min(Math.Min(batPow, residual), BatDischargeMax());
                else if (residual < 0) bat = -Math.Min(Math.Min(batPow, -residual), BatChargeMax());
                residual -= bat;
            }
            // governor (primary response of running dispatchable units)
            int gUp = 0, gDn = 0;
            for (int p = 0; p <= PHydro; p++)
            {
                sUp[p] = 0; sDn[p] = 0;
                if (POut[p] <= 0 || !PGov[p]) continue;
                int band = PCap[p] * Gd.GovernorPct / 100;
                sUp[p] = Math.Min(band, PCap[p] - POut[p]);
                if (p == PHydro) sUp[p] = (int)Math.Min(sUp[p], Math.Max(0, ResWh / WhPerKwStep - POut[p]));
                sDn[p] = Math.Min(band, POut[p] - (p != PHydro ? PMin[p] : 0));
                gUp += Math.Max(sUp[p], 0);
                gDn += Math.Max(sDn[p], 0);
            }
            for (int p = 0; p < 6; p++) sGen[p] = p <= PHydro ? POut[p] : 0;
            if (residual > 0 && gUp > 0)
            {
                int take = Math.Min(residual, gUp);
                for (int p = 0; p <= PHydro; p++) if (sUp[p] > 0) sGen[p] += (int)((long)take * sUp[p] / gUp);
                residual -= take;
            }
            else if (residual < 0 && gDn > 0)
            {
                int take = Math.Min(-residual, gDn);
                for (int p = 0; p <= PHydro; p++) if (sDn[p] > 0) sGen[p] -= (int)((long)take * sDn[p] / gDn);
                residual += take;
            }
            // curtail renewables on surplus
            int ren = availRen;
            if (residual < 0 && ren > 0)
            {
                int cut = Math.Min(-residual, ren);
                ren -= cut;
                residual += cut;
            }
            if (availRen > 0)
            {
                sGen[PWind] = (int)((long)ren * windAv / availRen);
                sGen[PSolar] = ren - sGen[PWind];
            }
            sGen[PBattery] = bat;
            BatOut = bat;
            int supply = sGen[0] + sGen[1] + sGen[2] + sGen[3] + sGen[4] + bat + imp;
            int needle = supply - demand;
            // clamped to +-100 %: with every district dark demand is 0 and the ratio exploded (9,496,000 ‰ in play,
            // 10-29). Every threshold (band / shed / over) sits well inside, so the outcomes are unchanged.
            int actualPm = (int)Math.Max(-1000L, Math.Min(1000L, (long)needle * 1000 / Math.Max(demand, 1)));
            // the needle shows what the city wants, held-back pickup load included; the relays act on what is drawn
            NeedlePm = heldKw == 0 ? actualPm : (int)Math.Max(-1000L, Math.Min(1000L, (long)(needle - heldKw) * 1000 / Math.Max(demand + heldKw, 1)));
            if (actualPm >= -Gd.ShedPct * 10)   // the returning load comes back only while the supply carries it
                for (int i = 0; i < DPickProg.Length; i++) if (PickOff(i) > 0) DPickProg[i]++;
            LastDemand = demand;
            LastSupply = supply;
            double servedFrac = 1.0;
            if (needle < 0 && demand > 0) servedFrac = (double)supply / demand;
            // deficit -> auto shed; surplus -> trip
            // a cold-load pickup is still settling: the relay clock waits (owner #183-2c) — it starts once the load is back
            if (actualPm < -Gd.ShedPct * 10) { if (!AnyPickup()) DeficitSteps++; Acc.DeficitSteps++; }
            else DeficitSteps = 0;
            if (DeficitSteps >= Gd.ShedAfterS * 4 && !TutorialSafe)
            {
                AutoShedOne();
                DeficitSteps = 0;
            }
            if (NeedlePm > Gd.OverPct * 10) { SurplusSteps++; Acc.SurplusSteps++; }
            else SurplusSteps = 0;
            if (SurplusSteps >= Gd.OverTripAfterS * 4 && !TutorialSafe)
            {
                var running = new List<int>();
                if (POut[PSteam] > 0) running.Add(PSteam);
                if (POut[PGas] > 0) running.Add(PGas);
                if (running.Count == 1)
                {
                    // the ONLY running unit never trips on over-supply: it runs itself back to what the city draws (owner
                    // #183-2b: no sudden death — with one plant a trip blacked out the whole city for 30-90 s, a typical
                    // player fell to 29-41 % lights on). The wasted power still counted; two or more units: one trips.
                    int p = running[0];
                    int other = LastSupply - POut[p];
                    int need = Math.Max(0, LastDemand - other);
                    int steps = Clamp((int)((long)need * 10 / Math.Max(PCap[p], 1)), 0, 10);
                    PSteps[p] = Math.Min(PSteps[p], steps);
                    Pending.RemoveAll(pc => pc[1] == p);
                    Emit("plant_runback").With("plant", GameData.Plants[p]).With("setpoint", PSteps[p]);
                }
                else if (running.Count > 1)
                {
                    int pick = running[(int)(Rand() * running.Count) % running.Count];
                    var pt = Gd.Event["plant_trip"];
                    int lo = pt.DurS[0], hi = pt.DurS[1];
                    TripPlant(pick, (lo + (int)(Rand() * (hi - lo + 1))) * 4, "over_supply");
                }
                SurplusSteps = 0;
            }
            // auto restore (relays reclose when the spinning reserve can carry the district)
            int batMax = bat;
            if (PLevel[PBattery] > 0 && BatMode == 3) batMax = BatDischargeMax();
            int room = thermal + gUp + availRen + imp + batMax - demand;
            if (Gd.AutoRestore)
            {
                int cand = -1;
                for (int i = 0; i < nd; i++)
                {
                    if (DHooked[i] && (DState[i] == AutoShed || DState[i] == Backup) && Route(i) >= 0)
                        if ((long)room * 100 >= (long)sReq[i] * (100 + Gd.RestoreMarginPct) && (cand < 0 || DPrio[i] > DPrio[cand])) cand = i;
                }
                if (cand >= 0 && NeedlePm >= -Gd.BandPct * 10)
                {
                    RestoreSteps++;
                    if (RestoreSteps >= Gd.RestoreAfterS * 4)
                    {
                        DState[cand] = Lit;
                        DBackupUntil[cand] = -1;
                        RestoreSteps = 0;
                        Acc.Restores++;
                        Emit("restore").With("district", DIds[cand]).With("auto", true);
                    }
                }
                else RestoreSteps = 0;
            }
            for (int i = 0; i < nd; i++)
            {
                if (DState[i] == Backup && DBackupUntil[i] <= s)
                {
                    DState[i] = AutoShed;
                    DBackupUntil[i] = -1;
                    Emit("backup_end").With("district", DIds[i]);
                }
            }
            // line flows + heat
            Array.Clear(sFlows, 0, sFlows.Length);
            int srv = 0;
            for (int i = 0; i < nd; i++)
            {
                if (sOn[i] > 0)
                {
                    int sv = (int)(sOn[i] * servedFrac);
                    srv += sv;
                    sFlows[sRt[i]] += sv;
                    DSrvWh[i] += (long)sv * WhPerKwStep;
                    if (i == Market && s >= FestFrom && s < FestTo) Acc.FestWh += (long)sv * WhPerKwStep;
                }
                if (DHooked[i])
                {
                    DDemWh[i] += (long)sReq[i] * WhPerKwStep;
                    if (DState[i] == Backup) DSrvWh[i] += (long)(sReq[i] * (1000 - DPart[i]) / 1000) * WhPerKwStep;
                    if ((DState[i] != Lit && DState[i] != Backup) || (DState[i] == Lit && sRt[i] < 0))
                    {
                        DDarkSteps[i]++;
                        if (i == Hospital && s >= TestFrom && s < TestTo) Acc.TestDark++;
                    }
                }
            }
            if (HilltopCorr >= 0 && sGen[PWind] > 0)
            {
                int wc = CorridorUp(HilltopCorr) ? HilltopCorr : CTie[HilltopCorr];
                if (wc >= 0) sFlows[wc] -= sGen[PWind];
            }
            double heatUp = Gd.LineHeatPerS / 4.0 * HeatFull;
            int cool = (int)(Gd.LineCoolPerS / 4.0 * HeatFull);
            for (int c = 0; c < CIds.Length; c++)
            {
                int fl = Math.Abs(sFlows[c]);
                CFlow[c] = sFlows[c];
                if (fl > CCap[c])
                {
                    CHeat[c] = Math.Min(HeatFull, CHeat[c] + (int)(heatUp * ((double)fl / CCap[c] - 1.0)) + 1);
                    if (CHeat[c] >= HeatFull)
                    {
                        if (TutorialSafe) CHeat[c] = HeatFull - 1;
                        else
                        {
                            COutUntil[c] = s + Gd.LineRecloseS * 4;
                            CHeat[c] = HeatFull / 2;
                            Acc.LineTrips++;
                            Emit("line_trip").With("corridor", CIds[c]);
                        }
                    }
                }
                else if ((long)fl * 10 < (long)CCap[c] * 9) CHeat[c] = Math.Max(0, CHeat[c] - cool);
            }
            // crews
            foreach (var cr in Crews)
            {
                int st = cr.State;
                if (st == CrewTruck.Travel && cr.Until <= s)
                {
                    var f = FindFault(cr.Job);
                    cr.State = CrewTruck.Repair;
                    if (f != null)
                    {
                        cr.Node = f.Site;
                        cr.Until = s + f.Repair;
                    }
                    Emit("crew_arrived").With("crew", cr.Id).With("fault", cr.Job);
                }
                else if (st == CrewTruck.Repair && cr.Until <= s)
                {
                    var f = FindFault(cr.Job);
                    if (f != null) RepairFault(f, false);
                    else { cr.State = CrewTruck.Idle; cr.Job = -1; }
                }
                else if (st == CrewTruck.Idle && cr.Node != Gd.Depot && cr.Until <= s)
                {
                    cr.State = CrewTruck.Return;
                    cr.Until = s + TravelSteps(cr.Node, Gd.Depot);
                }
                else if (st == CrewTruck.Return && cr.Until <= s)
                {
                    cr.State = CrewTruck.Idle;
                    cr.Node = Gd.Depot;
                }
            }
            // storage
            if (PLevel[PBattery] > 0)
            {
                if (bat > 0) BatSoc = Math.Max(0, BatSoc - (long)bat * WhPerKwStep * 100 / BatEff);
                else if (bat < 0) BatSoc = Math.Min(BatCapWh, BatSoc - (long)bat * WhPerKwStep * BatEff / 100);
            }
            if (PLevel[PHydro] > 0) ResWh = ClampL(ResWh + (long)(InflowKw[s] - sGen[PHydro]) * WhPerKwStep, 0, ResCapWh);
            // counters
            for (int p = 0; p < 5; p++) if (sGen[p] > 0) PGenWh[p] += (long)sGen[p] * WhPerKwStep;
            if (bat > 0) PGenWh[PBattery] += (long)bat * WhPerKwStep;
            Acc.DemWh += (long)(reqTotal - excusedKw) * WhPerKwStep;   // dark time already handled by a crew is not held against the player
            Acc.ServedWh += (long)(srv + backupKw) * WhPerKwStep;
            Acc.BackupWh += (long)backupKw * WhPerKwStep;
            if (needle > 0) Acc.WastedWh += (long)needle * WhPerKwStep;
            if (Math.Abs(NeedlePm) <= Gd.BandPct * 10) Acc.BandSteps++;
            if (s % Gd.StepsPerHour == 0)
            {
                int mask = 0;
                for (int i = 0; i < nd; i++) if (DHooked[i] && DState[i] == Lit && sRt[i] >= 0) mask |= 1 << i;
                Hourly.Add(new[] { reqTotal, supply, NeedlePm, mask });
            }
            Step = s + 1;
            if (Step >= Steps) Emit("shift_end");
        }

        void TripPlant(int p, int dur, string cause)
        {
            PTrip[p] = Step + dur;
            POut[p] = 0;
            Acc.Trips++;
            Emit("plant_trip").With("plant", GameData.Plants[p]).With("until", PTrip[p]).With("cause", cause);
        }

        void AutoShedOne()
        {
            int pick = -1;
            for (int i = 0; i < DIds.Length; i++)
            {
                if (!DHooked[i] || DState[i] != Lit || i == Hospital || Route(i) < 0) continue;
                if (pick < 0 || DPrio[i] < DPrio[pick]) pick = i;
            }
            if (pick >= 0)
            {
                DState[pick] = AutoShed;
                Acc.AutoShed++;
                Emit("shed").With("district", DIds[pick]).With("auto", true);
                return;
            }
            if (Hospital >= 0 && DHooked[Hospital] && DState[Hospital] == Lit)
            {
                DState[Hospital] = Backup;
                DBackupUntil[Hospital] = Step + Gd.HospitalBackupS * 4;
                Acc.BackupRuns++;
                Emit("backup_on").With("district", DIds[Hospital]).With("until", DBackupUntil[Hospital]);
            }
        }

        // ------------------------------------------------------------------------------------------- read-outs

        public bool IsLit(int i) => DHooked[i] && (DState[i] == Backup || (DState[i] == Lit && Route(i) >= 0));

        public double HourOfDay => (Plan.StartMin / 60.0 + (double)Step / Gd.StepsPerHour) % 24.0;

        /// <summary>Demand of the districts that are lit now, at step s (the forecast line on the needle card).</summary>
        public int DemandAt(int s)
        {
            s = Clamp(s, 0, Steps - 1);
            int tot = 0;
            for (int i = 0; i < DIds.Length; i++)
                if (DHooked[i] && DState[i] == Lit && Route(i) >= 0) tot += Dem[i][s] * Math.Max(0, 1000 - DPart[i] - PickOff(i)) / 1000;
            return tot;
        }

        public int RenewablesAt(int s)
        {
            s = Clamp(s, 0, Steps - 1);
            int w = PLevel[PWind] > 0 ? (int)(PCap[PWind] * WindCf[s]) : 0;
            return w + (PLevel[PSolar] > 0 ? (int)(PCap[PSolar] * SolarCf[s]) : 0);
        }

        public int SetpointAfterPending(int p)
        {
            int n = PSteps[p];
            foreach (var pc in Pending) if (pc[1] == p) n += pc[2];
            return Clamp(n, 0, 10);
        }

        public Carry MakeCarry()
        {
            var c = new Carry { GasOn = POn[PGas], BatSoc = BatSoc, BatMode = BatMode, ResWh = ResWh };
            for (int i = 0; i <= PHydro; i++) if (PLevel[i] > 0) c.Steps[GameData.Plants[i]] = SetpointAfterPending(i);
            return c;
        }

        // ------------------------------------------------------------------------------------------- save

        static List<object> L(int[] a) { var l = new List<object>(a.Length); foreach (var v in a) l.Add((long)v); return l; }
        static List<object> L(long[] a) { var l = new List<object>(a.Length); foreach (var v in a) l.Add(v); return l; }

        public JObj ToJson()
        {
            var pend = new List<object>();
            foreach (var pc in Pending) pend.Add(new List<object> { (long)pc[0], (long)pc[1], (long)pc[2] });
            var fl = new List<object>();
            foreach (var f in Faults) fl.Add(f.ToJson());
            var cl = new List<object>();
            foreach (var c in Crews) cl.Add(c.ToJson());
            var hr = new List<object>();
            foreach (var h in Hourly) hr.Add(L(h));
            return new JObj
            {
                ["step"] = (long)Step, ["tutorial_safe"] = TutorialSafe, ["d_route"] = L(DRoute), ["d_state"] = L(DState), ["d_part"] = L(DPart), ["d_backup_until"] = L(DBackupUntil),
                ["d_dark_steps"] = L(DDarkSteps), ["d_dem_wh"] = L(DDemWh), ["d_srv_wh"] = L(DSrvWh), ["d_pick_at"] = L(DPickAt), ["d_pick_part"] = L(DPickPart), ["d_pick_prog"] = L(DPickProg), ["p_steps"] = L(PSteps), ["p_out"] = L(POut), ["p_on"] = L(POn),
                ["p_ready"] = L(PReady), ["p_trip"] = L(PTrip), ["p_gen_wh"] = L(PGenWh), ["pending"] = pend, ["bat_soc"] = BatSoc, ["bat_mode"] = (long)BatMode,
                ["bat_out"] = (long)BatOut, ["res_wh"] = ResWh, ["c_heat"] = L(CHeat), ["c_out_until"] = L(COutUntil), ["c_fault"] = L(CFault), ["c_flow"] = L(CFlow),
                ["faults"] = fl, ["crews"] = cl, ["next_fault"] = (long)NextFault, ["fault_ptr"] = (long)FaultPtr, ["mark_ptr"] = (long)MarkPtr,
                ["deficit_steps"] = (long)DeficitSteps, ["surplus_steps"] = (long)SurplusSteps, ["restore_steps"] = (long)RestoreSteps, ["dr_until"] = (long)DrUntil,
                ["dr_cd_until"] = (long)DrCdUntil, ["import_until"] = (long)ImportUntil, ["import_kw"] = (long)ImportKw, ["rng"] = RngState, ["needle_pm"] = (long)NeedlePm,
                ["last_demand"] = (long)LastDemand, ["last_supply"] = (long)LastSupply, ["acc"] = Acc.ToJson(), ["hourly"] = hr
            };
        }

        static bool ReadInts(object o, string key, int n, out int[] a)
        {
            a = null;
            var l = J.Arr(o, key);
            if (l == null || l.Count != n) return false;
            a = new int[n];
            for (int i = 0; i < n; i++) { if (!J.IsInt(l[i])) return false; a[i] = J.I(l[i]); }
            return true;
        }

        static bool ReadLongs(object o, string key, int n, out long[] a)
        {
            a = null;
            var l = J.Arr(o, key);
            if (l == null || l.Count != n) return false;
            a = new long[n];
            for (int i = 0; i < n; i++) { if (!J.IsInt(l[i])) return false; a[i] = J.L(l[i]); }
            return true;
        }

        /// <summary>Rebuilds a live shift from its plan + context (static part) and a ToJson() snapshot. Returns "" or an error code.</summary>
        public string RestoreFrom(GameData gd, ShiftPlan plan, ShiftContext ctx, JObj d)
        {
            Gd = gd;
            Plan = plan;
            Static(ctx);
            int nd = DIds.Length, nc = CIds.Length, np = 6;
            if (d == null) return "invalid_shift";
            if (!ReadInts(d, "d_state", nd, out DState) || !ReadInts(d, "d_part", nd, out DPart) || !ReadInts(d, "d_backup_until", nd, out DBackupUntil)
                || !ReadInts(d, "d_dark_steps", nd, out DDarkSteps) || !ReadLongs(d, "d_dem_wh", nd, out DDemWh) || !ReadLongs(d, "d_srv_wh", nd, out DSrvWh))
                return "invalid_shift_districts";
            if (J.Arr(d, "d_pick_at") == null) { DPickAt = Fill(nd, -1); DPickPart = new int[nd]; DPickProg = new int[nd]; }   // saves from before the pickup
            else if (!ReadInts(d, "d_pick_at", nd, out DPickAt) || !ReadInts(d, "d_pick_part", nd, out DPickPart) || !ReadInts(d, "d_pick_prog", nd, out DPickProg)) return "invalid_shift_districts";
            if (!ReadInts(d, "p_steps", np, out PSteps) || !ReadInts(d, "p_out", np, out POut) || !ReadInts(d, "p_on", np, out POn)
                || !ReadInts(d, "p_ready", np, out PReady) || !ReadInts(d, "p_trip", np, out PTrip) || !ReadLongs(d, "p_gen_wh", np, out PGenWh))
                return "invalid_shift_plants";
            if (!ReadInts(d, "c_heat", nc, out CHeat) || !ReadInts(d, "c_out_until", nc, out COutUntil) || !ReadInts(d, "c_fault", nc, out CFault)
                || !ReadInts(d, "c_flow", nc, out CFlow))
                return "invalid_shift_lines";
            Step = J.I(d, "step", -1);
            if (Step < 0 || Step > Steps) return "invalid_shift_step";
            TutorialSafe = J.B(d, "tutorial_safe");
            foreach (var v in DState) if (v < 0 || v > 3) return "invalid_shift_district_state";
            foreach (var v in PSteps) if (v < 0 || v > 10) return "invalid_shift_setpoint";
            foreach (var v in CHeat) if (v < 0 || v > HeatFull) return "invalid_shift_heat";
            Pending = new List<int[]>();
            var pl = J.Arr(d, "pending");
            if (pl == null || pl.Count > 256) return "invalid_shift_pending";
            foreach (var x in pl)
            {
                if (!(x is List<object> row) || row.Count != 3) return "invalid_shift_pending";
                int p = J.I(row[1], -1);
                if (p < 0 || p > PHydro) return "invalid_shift_pending";
                Pending.Add(new[] { J.I(row[0]), p, J.I(row[2]) });
            }
            BatSoc = J.L(d, "bat_soc"); BatMode = J.I(d, "bat_mode", 3); BatOut = J.I(d, "bat_out"); ResWh = J.L(d, "res_wh");
            if (BatMode < 0 || BatMode > 3 || BatSoc < 0 || BatSoc > BatCapWh || ResWh < 0 || ResWh > ResCapWh) return "invalid_shift_storage";
            Faults = new List<Fault>();
            var fa = J.Arr(d, "faults");
            if (fa == null || fa.Count > 512) return "invalid_shift_faults";
            foreach (var x in fa)
            {
                if (!(x is JObj)) return "invalid_shift_fault";
                var f = Fault.FromJson(x);
                if (f.Corridor >= nc || f.District >= nd || !gd.RoadDist.ContainsKey(f.Site) || f.Crew >= CrewsN) return "invalid_shift_fault";
                Faults.Add(f);
            }
            Crews = new List<CrewTruck>();
            var ca = J.Arr(d, "crews");
            if (ca == null) return "invalid_shift_crews";
            foreach (var x in ca)
            {
                if (!(x is JObj)) return "invalid_shift_crew";
                var c = CrewTruck.FromJson(x);
                if (!gd.RoadDist.ContainsKey(c.Node) || c.State < 0 || c.State > 3) return "invalid_shift_crew";
                Crews.Add(c);
            }
            if (Crews.Count != CrewsN) return "invalid_shift_crews";
            for (int i = 0; i < Crews.Count; i++) if (Crews[i].Id != i) return "invalid_shift_crews";
            NextFault = J.I(d, "next_fault", 1); FaultPtr = J.I(d, "fault_ptr"); MarkPtr = J.I(d, "mark_ptr");
            if (FaultPtr < 0 || FaultPtr > Plan.Faults.Count || MarkPtr < 0 || MarkPtr > marks.Count) return "invalid_shift_pointers";
            DeficitSteps = J.I(d, "deficit_steps"); SurplusSteps = J.I(d, "surplus_steps"); RestoreSteps = J.I(d, "restore_steps");
            DrUntil = J.I(d, "dr_until", -1); DrCdUntil = J.I(d, "dr_cd_until", -1); ImportUntil = J.I(d, "import_until", -1); ImportKw = J.I(d, "import_kw");
            RngState = J.L(d, "rng");
            if (RngState < 1 || RngState >= 2147483647L) return "invalid_shift_rng";
            NeedlePm = J.I(d, "needle_pm"); LastDemand = J.I(d, "last_demand"); LastSupply = J.I(d, "last_supply");
            Acc = ShiftAcc.FromJson(J.Get(d, "acc"));
            Hourly = new List<int[]>();
            var ha = J.Arr(d, "hourly");
            if (ha != null)
                foreach (var x in ha)
                    if (x is List<object> row && row.Count == 4) Hourly.Add(new[] { J.I(row[0]), J.I(row[1]), J.I(row[2]), J.I(row[3]) });
            Events.Clear();
            // Recomputing here can advance the route cache across a line trip/expiry before the next operator command.
            // New saves preserve the exact last-step cache; older saves retain the previous reconstruction behavior.
            if (d.ContainsKey("d_route"))
            {
                if (!ReadInts(d, "d_route", nd, out DRoute)) return "invalid_shift_routes";
                for (int i = 0; i < nd; i++)
                    if (DRoute[i] < -1 || DRoute[i] >= nc ||
                        (DRoute[i] >= 0 && (!DHooked[i] || (DRoute[i] != DCorr[i] && DRoute[i] != CTie[DCorr[i]]))))
                        return "invalid_shift_routes";
            }
            else Routes(Step);
            return "";
        }
    }
}
