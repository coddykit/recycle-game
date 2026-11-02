// Volt City — the 9-step guide (DESIGN section 9, games/CLAUDE.md section 9). Engine-free state machine; the UI draws the
// spotlight on the control whose Target id matches and feeds every tap back:
//   * one action per step; the step advances only when the player really did THAT action (GameActions.Ran);
//   * time is stopped while a step waits (sim paused; step 4: the truck drives, nothing else runs) and the guide's shifts
//     run TutorialSafe (no shedding, no trips, no bad events) — nothing can be lost;
//   * a tap outside the spotlight never acts and never stalls the guide: it is ONE wrong tap (counter + flash), however the
//     device delivered it (the UI calls WrongTap once per physical tap: pointer-up only, world taps through TapFilter);
//   * Skip always works; Settings -> "Replay guide" restarts it (onboarding state is a setting, not "a save exists");
//   * a relaunch mid-guide re-derives the step from the career state (never locks: missing fault -> re-scripted, a phase
//     that has passed -> the step is skipped, an item already owned / unaffordable -> skipped).
using System;
using System.Collections.Generic;
using VoltCity.Sim;

namespace VoltCity.Game
{
    public sealed class Coach
    {
        public const int Steps = 9;
        public const int Done = 10;

        public static readonly string[] Targets =
        {
            "",
            "grid.steam.plus",          // 1 ramp up Old Smoky
            "hud.needle",               // 2 tap the needle (explainer)
            "world.fault",              // 3 tap the spark (fault card opens)
            "card.fault.send",          // 4 send the crew
            "report.collect",           // 5 collect the shift profit
            "build.line_riverside_1.buy", // 6 buy Riverside line I ($420)
            "build.gas_1.buy",          // 7 build the gas peaker
            "grid.gas.start",           // 8 start the peaker in the 2nd evening peak
            "city.hilltop",             // 9 open Hilltop (the next hookup goal)
        };

        /// <summary>Tab the step needs open ("" = none / not a tab control).</summary>
        public static readonly string[] TabOf = { "", "grid", "", "", "", "", "build", "build", "grid", "city" };

        public int Step { get; private set; }
        public bool Active { get; private set; }
        /// <summary>The spotlight is up and the game waits for the step's action.</summary>
        public bool Showing { get; private set; }
        public int WrongTaps { get; private set; }
        /// <summary>Increments on every wrong tap (the UI flashes the ring when it changes).</summary>
        public int Flash { get; private set; }
        /// <summary>The sim must be paused (the UI combines this with ad / app pauses: sim.SetPaused(any)).</summary>
        public bool WantsPause { get; private set; }
        /// <summary>Fault the spotlight points at (step 3/4).</summary>
        public int FaultId { get; private set; } = -1;

        public Action<string, Dictionary<string, object>> Analytics = (n, p) => { };
        /// <summary>Persist the resume point (GameSettings.CoachStep / CoachDone).</summary>
        public Action<int, bool> Persist = (step, done) => { };

        int scriptAt = -1;      // grid step when the guide's spark is scripted (step 3)
        int readyAt = -1;       // grid step from which step 1 may show (5 s into the shift)
        bool scripted;

        /// <summary>Balance assist (P1 naive-coach model, DESIGN section 9 context coaching): between the numbered steps of the
        /// guide the spotlight follows the needle ("still short: tap + again") and, like a step, stops time until tapped.
        /// After the guide, in chapter 1, the same assist is SOFT: ring + bubble only, no dim, no pause, no gate.</summary>
        public bool AssistOn { get; private set; }
        public bool AssistSoft { get; private set; }
        public string AssistTarget { get; private set; } = "";
        public string AssistKey { get; private set; } = "";
        /// <summary>UI sets this: soft assists allowed (guide done, chapter 1).</summary>
        public bool SoftAssists;
        int assistQuietUntil = -1;

        /// <summary>A blocking spotlight is up (numbered step or guide assist): taps outside it are wrong taps.</summary>
        public bool Spot => Showing || (AssistOn && !AssistSoft);

        public string Target => Showing && Step >= 1 && Step <= Steps ? Targets[Step] : AssistOn ? AssistTarget : "";
        public string Tab => Showing && Step >= 1 && Step <= Steps ? TabOf[Step] : AssistOn ? "grid" : "";
        public string BubbleKey => Showing ? "coach." + Step : AssistKey;

        /// <summary>Supply once every queued tap has landed: dispatchable units at their after-pending setpoints (min
        /// output while running) + what wind / sun / battery give now. The assist asks for exactly as many taps as
        /// that projection needs — never a tap while enough is already on its way (P1 tuning: no oscillation).</summary>
        public static long ProjectedKw(Grid g)
        {
            long kw = 0;
            for (int p = 0; p < g.PLevel.Length; p++)
            {
                if (g.PLevel[p] == 0 || g.PTrip[p] > g.Step) continue;
                if (p <= Grid.PHydro)
                {
                    bool on = g.POn[p] == 1;
                    if (on) kw += Math.Max(g.PMin[p], (long)g.SetpointAfterPending(p) * g.PCap[p] / 10);
                }
                else kw += Math.Max(0, g.POut[p]);
            }
            return kw;
        }

        /// <summary>The control that brings the needle back to green now (target, bubble key), or null.</summary>
        /// <summary>How many "+" taps on that plant close today's gap (the assist says it once — device play 10-29: four
        /// "Tap + again" tips in a row, each stopping the clock).</summary>
        public int AssistTaps { get; private set; } = 1;

        /// <summary>The most important district the grid switched off itself (Grid.AutoShed), or -1.</summary>
        public static int DarkToRestore(Grid g)
        {
            int cand = -1;
            for (int i = 0; i < g.DIds.Length; i++)
                if (g.DHooked[i] && g.DState[i] == Grid.AutoShed && (cand < 0 || g.DPrio[i] > g.DPrio[cand])) cand = i;
            return cand;
        }

        /// <summary>What the district needs from the plants to come back: its demand now + the grid's restore margin.</summary>
        public static long RestoreNeedKw(Grid g, int i)
        {
            int s = Math.Min(g.Step, g.Steps - 1);
            return (long)g.Dem[i][s] * (100 + g.Gd.RestoreMarginPct + 1) / 100;
        }

        /// <summary>The district the restore assist is about (for the bubble's {district}).</summary>
        public string AssistDistrict { get; private set; } = "";

        public static int TapsNeeded(Grid g, int plant)
        {
            long step = Math.Max(1, g.PCap[plant] / 10);
            long gap = g.LastDemand * 99 / 100 - ProjectedKw(g);
            int left = 10 - g.SetpointAfterPending(plant);
            return (int)Math.Max(1, Math.Min(left, (gap + step - 1) / step));
        }

        public static (string target, string key)? AssistFor(Simulation sim)
        {
            var g = sim.Grid;
            if (g == null) return null;
            long demand = g.LastDemand, proj = ProjectedKw(g);
            bool steamOk = g.PLevel[Grid.PSteam] > 0 && g.PTrip[Grid.PSteam] <= g.Step;
            bool gasOk = g.PLevel[Grid.PGas] > 0 && g.PTrip[Grid.PGas] <= g.Step;
            if (g.NeedlePm < -30 && proj < demand * 99 / 100)
            {
                if (steamOk && g.SetpointAfterPending(Grid.PSteam) < 10) return ("grid.steam.plus", "coach.assist_up");
                if (gasOk)
                {
                    if (g.POn[Grid.PGas] == 0) return ("grid.gas.start", "coach.assist_gas");
                    if (g.SetpointAfterPending(Grid.PGas) < 10) return ("grid.gas.plus", "coach.assist_up_gas");
                }
            }
            else if (DarkToRestore(g) is int dark && dark >= 0 && g.NeedlePm >= -30)
            {
                // the grid switched a district off and the needle now reads balanced: nothing says "short", but it only
                // comes back when the running plants have room for it (Grid auto restore). Say so and point at "+".
                // Play 10-29: 4 of 4 shifts at 1 star (lights on 22-45 %) with districts left dark all shift.
                long want = demand + RestoreNeedKw(g, dark);
                if (proj < want)
                {
                    if (steamOk && g.SetpointAfterPending(Grid.PSteam) < 10) return ("grid.steam.plus", "coach.assist_restore");
                    if (gasOk && g.POn[Grid.PGas] == 0) return ("grid.gas.start", "coach.assist_gas");
                    if (gasOk && g.SetpointAfterPending(Grid.PGas) < 10) return ("grid.gas.plus", "coach.assist_up_gas");
                }
            }
            else if (g.NeedlePm > 30 && proj > demand * 105 / 100 && AllLit(g))
            {
                // never "turn down" while a district is dark (a fault / trip): that surplus comes back with its lights
                long step;
                if (gasOk && g.POn[Grid.PGas] == 1 && g.SetpointAfterPending(Grid.PGas) > 0)
                {
                    step = g.PCap[Grid.PGas] / 10;
                    if (proj - step >= demand) return ("grid.gas.minus", "coach.assist_down_gas");
                }
                step = g.PCap[Grid.PSteam] / 10;
                if (steamOk && g.SetpointAfterPending(Grid.PSteam) > 0 && proj - step >= demand) return ("grid.steam.minus", "coach.assist_down");
            }
            return null;
        }

        int repairAt = -1;

        /// <summary>DESIGN section 9 step 4: "time runs only for the truck" — the guide's own spark is mended ~2 s after the
        /// truck arrives (an instant repair on the sim's own command, guide shifts only), so the lesson costs the newbie
        /// seconds of darkness, not the 8 s a storm fault takes later.</summary>
        void TutorialRepair(Simulation sim)
        {
            var g = sim.Grid;
            if (g == null || !sim.TutorialSafe || FaultId < 0 || Step < 4) return;
            var f = g.FindFault(FaultId);
            if (f == null || !f.Open || f.Cause != "tutorial" || f.Crew < 0) { repairAt = -1; return; }
            if (g.Crews[f.Crew].State != CrewTruck.Repair) return;
            if (repairAt < 0) repairAt = g.Step + 8;
            if (g.Step >= repairAt && sim.Queue(Cmd.Overtime(f.Id)).Ok) repairAt = int.MaxValue;
        }

        static bool AllLit(Grid g)
        {
            for (int i = 0; i < g.DIds.Length; i++) if (g.DHooked[i] && (!g.IsLit(i) || g.DPart[i] > 0)) return false;
            return true;
        }

        void UpdateAssist(Simulation sim)
        {
            var g = sim.Grid;
            bool live = sim.S.Phase == "live" && g != null;
            bool may = live && !Showing && (Active ? Step > 1 : SoftAssists && sim.S.Chapter == 1) && g.Step >= assistQuietUntil;
            var a = may ? AssistFor(sim) : null;
            if (a == null)
            {
                // a blocking assist stays until tapped (its condition can only clear through the tap: time is stopped)
                if (AssistOn && !AssistSoft && live && !Showing && Active) return;
                AssistOn = false;
                return;
            }
            if (!AssistOn || AssistTarget != a.Value.target) Analytics("onboarding_assist", new Dictionary<string, object> { { "target", a.Value.target } });
            AssistOn = true;
            AssistSoft = !Active;
            AssistTarget = a.Value.target;
            AssistKey = a.Value.key;
            AssistTaps = a.Value.target == "grid.steam.plus" ? TapsNeeded(g, Grid.PSteam) : 1;
            int dk = a.Value.key == "coach.assist_restore" ? DarkToRestore(g) : -1;
            AssistDistrict = dk >= 0 ? g.DIds[dk] : "";
        }

        /// <summary>Starts (or resumes) the guide. resumeStep: GameSettings.CoachStep (0 = from the start).</summary>
        public void Begin(Simulation sim, int resumeStep = 0)
        {
            Active = true;
            Showing = false;
            Step = Math.Max(1, Math.Min(Steps, resumeStep <= 0 ? 1 : resumeStep));
            FaultId = -1;
            scripted = false;
            scriptAt = readyAt = -1;
            sim.TutorialSafe = true;
            Persist(Step, false);
        }

        public void Skip(Simulation sim)
        {
            if (!Active) return;
            Analytics("onboarding_skip", new Dictionary<string, object> { { "step", (long)Step } });
            Finish(sim, false);
        }

        void Finish(Simulation sim, bool completed)
        {
            Active = false;
            Showing = false;
            AssistOn = false;
            WantsPause = false;
            sim.TutorialSafe = false;
            if (completed) Analytics("onboarding_complete", new Dictionary<string, object> { { "wrong_taps", (long)WrongTaps } });
            Step = Done;
            Persist(Done, true);
        }

        void Advance(Simulation sim)
        {
            Analytics("onboarding_step", new Dictionary<string, object> { { "step", (long)Step } });
            Showing = false;
            WantsPause = false;
            Step++;
            if (Step > Steps) { Finish(sim, true); return; }
            Persist(Step, false);
            Update(sim);
        }

        static bool OpenFault(Simulation sim, out int id)
        {
            id = -1;
            var g = sim.Grid;
            if (g == null) return false;
            foreach (var f in g.Faults) if (f.Open && f.Crew < 0) { id = f.Id; return true; }
            return false;
        }

        /// <summary>Every frame (cheap): shows the current step when its moment has come, skips steps whose moment has
        /// passed, keeps the sim paused while a step waits.</summary>
        public void Update(Simulation sim)
        {
            if (!Active)
            {
                UpdateAssist(sim);
                WantsPause = false;
                return;
            }
            sim.TutorialSafe = true;
            var s = sim.S;
            var g = sim.Grid;
            string phase = s.Phase;
            // skip steps whose phase has passed (relaunch / replay in a later career state)
            for (int guard = 0; guard < Steps + 1 && Active; guard++)
            {
                bool skip = false;
                if (Step <= 4 && phase != "live" && !(Step == 4 && Showing)) skip = phase == "report" || (phase == "idle" && s.ShiftNo >= 1);
                if (Step == 5 && phase == "idle" && s.ShiftNo >= 1 && !Showing) skip = true;
                if (Step == 5 && phase == "live" && s.ShiftNo >= 1) skip = true;
                if (Step == 6 && (s.Owned.ContainsKey("line_riverside_1") || (phase == "idle" && Economy.WhyNot(sim, "line_riverside_1") != "") || phase == "live")) skip = true;
                if (Step == 7 && (s.Owned.ContainsKey("gas_1") || (phase == "idle" && Economy.WhyNot(sim, "gas_1") != "") || (phase == "live" && s.Level("gas") == 0))) skip = true;
                if (Step == 8 && (s.Level("gas") == 0 || (g != null && g.POn[Sim.Grid.PGas] == 1 && !Showing))) skip = true;
                if (!skip) break;
                Showing = false;
                Step++;
                if (Step > Steps) { Finish(sim, true); return; }
                Persist(Step, false);
            }
            if (!Showing)
            {
                bool ready = false;
                switch (Step)
                {
                    case 1:
                        if (phase == "live" && g != null)
                        {
                            // the evening peak is already biting when the shift opens (DESIGN section 4); the first seconds of
                            // the hook (camera settling over the dusk city) play while time waits for this step
                            if (readyAt < 0) readyAt = g.Step + 1;
                            ready = g.Step >= readyAt && s.Level("steam") > 0;
                        }
                        break;
                    case 2:
                        ready = phase == "live";
                        break;
                    case 3:
                        if (phase == "live" && g != null)
                        {
                            if (OpenFault(sim, out int fid)) { FaultId = fid; ready = true; }
                            else if (!scripted)
                            {
                                // the storm's spark comes once the player has the needle back near green (one lesson at a
                                // time), at the latest ~20 s after the needle step
                                if (scriptAt < 0) scriptAt = g.Step + 16;
                                bool calm = g.NeedlePm >= -30 || g.Step >= scriptAt + 64;
                                if (g.Step >= scriptAt && calm && sim.Queue(Cmd.ScriptFault("c_riverside")).Ok) scripted = true;
                            }
                        }
                        break;
                    case 4:
                    {
                        if (phase != "live" || g == null) break;
                        var f = g.FindFault(FaultId);
                        if (f != null && f.Open && f.Crew < 0) ready = true;
                        else if (f != null && f.Crew >= 0) { Advance(sim); return; }      // sent from the Crews tab instead
                        else if (OpenFault(sim, out int other)) { FaultId = other; ready = true; }
                        else { Step = 3; scripted = false; scriptAt = -1; Persist(Step, false); }  // relaunch: the spark is scripted again
                        break;
                    }
                    case 5: ready = phase == "report"; break;
                    case 6: ready = phase == "idle"; break;
                    case 7: ready = phase == "idle"; break;
                    case 8:
                        if (phase == "live" && g != null && s.Level("gas") > 0 && g.POn[Sim.Grid.PGas] == 0)
                            ready = (g.HourOfDay >= 17.75 && g.HourOfDay < 22) || g.NeedlePm <= -30;
                        break;
                    case 9: ready = true; break;
                }
                if (ready) Showing = true;
            }
            TutorialRepair(sim);
            UpdateAssist(sim);
            // time stops while the step (or a guide assist) waits (only a live shift has time to stop)
            WantsPause = Spot && phase == "live";
        }

        /// <summary>A tap on control `target` while the spotlight is up. True = let it act (the spotlight target or nothing
        /// is showing); false = it was a wrong tap (counted ONCE, the ring flashes, nothing happens in the game).</summary>
        public bool Tap(string target)
        {
            if (!Spot) return true;
            if (Matches(target)) return true;
            WrongTap();
            return false;
        }

        public bool Matches(string target)
        {
            if (string.IsNullOrEmpty(target)) return false;
            string want = Target;
            if (target == want) return true;
            // step 3: the spark in the world or its row in the Crews tab open the same fault card
            if (Step == 3 && (target == "crews.fault" || target == "crews.fault.send")) return true;
            return false;
        }

        /// <summary>Action id of an assist target.</summary>
        public static string AssistAction(string target)
        {
            switch (target)
            {
                case "grid.steam.plus": return "ramp:steam:+1";
                case "grid.steam.minus": return "ramp:steam:-1";
                case "grid.gas.start": return "power:gas:on";
                case "grid.gas.plus": return "ramp:gas:+1";
                case "grid.gas.minus": return "ramp:gas:-1";
            }
            return "";
        }

        public void WrongTap()
        {
            WrongTaps++;
            Flash++;
        }

        /// <summary>GameActions.Ran observer: advances when the step's own action succeeded.</summary>
        public void OnAction(Simulation sim, string id, ActionResult r)
        {
            if (r == null || !r.Ok) return;
            if (AssistOn && id == AssistAction(AssistTarget))
            {
                AssistOn = false;
                assistQuietUntil = (sim.Grid?.Step ?? 0) + 1;
                if (!Showing) WantsPause = false;
            }
            if (!Active || !Showing) return;   // a step advances only after it was shown (its own tap)
            bool hit = false;
            switch (Step)
            {
                case 1: hit = id.StartsWith("ramp:steam:+"); break;
                case 2: hit = id == "open:needle"; break;
                case 3:
                    if (id.StartsWith("select:")) { hit = true; int.TryParse(id.Substring(7), out int f); FaultId = f; }
                    break;
                case 4: hit = id.StartsWith("send:"); break;
                case 5: hit = id == "collect"; break;
                case 6: hit = id == "buy:line_riverside_1"; break;
                case 7: hit = id == "buy:gas_1"; break;
                case 8: hit = id == "power:gas:on"; break;
                case 9: hit = id == "card:district:hilltop"; break;
            }
            if (hit) Advance(sim);
        }
    }
}
