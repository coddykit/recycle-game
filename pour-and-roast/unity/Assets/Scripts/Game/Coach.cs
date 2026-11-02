// Pour & Roast — the 14-step show-and-do guide (design §9, fleet §9). One thing per step; the target is spotlit; the step
// advances ONLY when the player really does that action; a wrong tap repeats the hint (no penalty); time is paused while
// the guide waits for the player, and runs fast while the guide waits for the world (the van driving, the count line,
// the baler). Safe: the sim is in tutorial mode (no crises, no rating loss). Skip + Settings -> "Replay the guide".
// Volt City lesson (11-01, 1★ "guide error"): every step is recoverable — leaving the screen / relaunching mid-step
// re-derives the step from the career (Sync), never locks the player.
//   1 tap the machine (fill bar)        8 tap the coins (fee per bottle)
//   2 tap the van                       9 tap the baler
//   3 tap the machine (add to route)   10 sell the first bale
//   4 add the second machine           11 tap the fuel badge (km cost)
//   5 Go                               12 set a rule: empty at 80 %
//   6 watch the van (sped up)          13 buy a bigger bin
//   7 tap the depot                    14 hire a second driver
using System;
using System.Collections.Generic;
using PourAndRoast.Sim;

namespace PourAndRoast.Game
{
    public sealed class Coach
    {
        public const int Steps = 14;
        public const int Done = 15;

        /// <summary>What the UI spotlights per step (world: point:i / truck:i / depot / baler / coins; HUD: go / fuel; sheets:
        /// policy / upgrade / hire). Step 6 has no tap target (the guide waits for the van).</summary>
        public static readonly string[] Targets =
        {
            "", "point:0", "truck:0", "point:0", "point:1", "go", "truck:0", "depot", "coins", "baler", "sell:0", "fuel", "policy:1", "upgrade:1", "hire",
        };

        /// <summary>The action id (prefix) that completes each step.</summary>
        public static readonly string[] Need =
        {
            "", "select_point:0", "select_truck:0", "select_point:0", "select_point:1", "go", "", "depot", "till", "baler", "sell:0", "fuel_info",
            "policy:", "buy:bin_m:", "buy:driver",
        };

        public int Step { get; private set; }
        public bool Active { get; private set; }
        public int WrongTaps { get; private set; }
        /// <summary>Increments on a wrong tap: the UI pulses the hint again.</summary>
        public int Flash { get; private set; }
        public Action<string, Dictionary<string, object>> Analytics = (n, p) => { };
        public Action<int, bool> Persist = (step, done) => { };
        public string Target => Active && Step >= 1 && Step <= Steps ? Targets[Step] : "";
        public string BubbleKey => Active && Step >= 1 && Step <= Steps ? "coach." + Step : "";

        /// <summary>True while the guide waits for the world, not the player: the sim runs fast.</summary>
        public bool FastForward(Simulation sim)
        {
            if (!Active) return false;
            if (Step == 6) return true;
            if (Step == 8) return sim.S.Depot.Till < 1;
            if (Step == 10) return sim.S.Depot.Bales[Mats.Pet] <= 0;
            return false;
        }

        /// <summary>True while the guide waits for the player: the sim is paused.</summary>
        public bool WantsPause(Simulation sim) => Active && !FastForward(sim);

        public void Begin(Simulation sim, GameActions actions, int resumeStep = 0)
        {
            Active = true;
            Step = Math.Max(1, Math.Min(Steps, resumeStep));
            if (Step == 1) Analytics("onboarding_begin", new Dictionary<string, object>());
            Sync(sim, actions);
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
            sim.EndTutorial();
            sim.SetPaused(false);
            if (completed) Analytics("onboarding_complete", new Dictionary<string, object> { { "wrong_taps", (long)WrongTaps } });
            Step = Done;
            Persist(Done, true);
        }

        /// <summary>GameActions.Allowed: during the guide only the step's action (and harmless UI) may run.</summary>
        public bool Allows(string action)
        {
            if (!Active) return true;
            if (action == "open" || action == "pause" || action == "resume") return true;
            string need = Step >= 1 && Step <= Steps ? Need[Step] : "";
            return need != "" && action.StartsWith(need, StringComparison.Ordinal);
        }

        public bool Matches(string action)
        {
            string need = Step >= 1 && Step <= Steps ? Need[Step] : "";
            return need != "" && action != null && action.StartsWith(need, StringComparison.Ordinal);
        }

        /// <summary>A tap the guide did not ask for: hint again, no penalty.</summary>
        public void WrongTap()
        {
            if (!Active) return;
            WrongTaps++;
            Flash++;
        }

        /// <summary>GameActions.Ran listener.</summary>
        public void OnAction(Simulation sim, GameActions actions, string id, ActionResult r)
        {
            if (!Active) return;
            if (r.Error == "blocked") { WrongTap(); return; }
            if (!r.Ok || !Matches(id)) return;
            if (Step == 5 && sim.Phase == Phases.Plan) sim.StartDay();
            Advance(sim, actions);
        }

        void Advance(Simulation sim, GameActions actions)
        {
            Analytics("onboarding_step", new Dictionary<string, object> { { "step", (long)Step } });
            Step++;
            if (Step > Steps) { Finish(sim, true); return; }
            Sync(sim, actions);
            Persist(Step, false);
        }

        /// <summary>Every frame: runs the world while the guide waits for it, advances step 6, keeps the day alive.</summary>
        public void Drive(Simulation sim, GameActions actions, double realDt)
        {
            if (!Active) return;
            if (sim.Phase == Phases.Report) { sim.NextDay(); }
            if (sim.Phase == Phases.Plan && Step >= 6) sim.StartDay();
            if (FastForward(sim))
            {
                sim.SetPaused(false);
                sim.AdvanceMinutes(Math.Max(0, realDt) * 40.0);   // ~100 game minutes per real second
            }
            else sim.SetPaused(true);
            if (Step == 6)
            {
                var t = sim.S.Trucks[0];
                bool heading = t.Stops.Count == 0 && t.Target < 0;
                if (heading || !t.Busy) Advance(sim, actions);
            }
            else Sync(sim, actions);
        }

        /// <summary>Recovery: re-derives a step whose preconditions are gone (relaunch, the screen was left, the day moved
        /// on). Never sends the player forward past an action they still have to learn, never locks them.</summary>
        public void Sync(Simulation sim, GameActions actions)
        {
            if (!Active) return;
            var t0 = sim.S.Trucks.Count > 0 ? sim.S.Trucks[0] : null;
            int guard = 0;
            while (guard++ < Steps)
            {
                int before = Step;
                switch (Step)
                {
                    case 3:
                    case 4:
                    case 5:
                        if (t0 != null && t0.Busy) { Step = 6; break; }            // already sent (relaunch after Go)
                        if (actions == null || actions.SelectedTruck != 0) Step = 2;    // the draft is UI state: pick the van again
                        else if (Step == 4 && !actions.Draft.Contains(0)) Step = 3;
                        else if (Step == 5 && actions.Draft.Count == 0) Step = 3;
                        break;
                    case 6:
                        if (t0 != null && !t0.Busy)
                        {
                            bool served = sim.S.Points.Count > 0 && sim.S.Points[0].LastServiceDay == sim.S.Day && sim.S.Points[0].Picked > 0;
                            Step = served ? 7 : 2;
                        }
                        break;
                    case 8:
                        // nothing will ever reach the till (fees banked at a day end meanwhile): let the tap pass
                        if (sim.S.Depot.Till < 1 && sim.S.Depot.IntakeTotal <= 0 && (t0 == null || !t0.Busy)) sim.S.Depot.Till = Math.Max(sim.S.Depot.Till, 1);
                        break;
                    case 10:
                        if (sim.S.Depot.Bales[Mats.Pet] <= 0 && !PetComing(sim)) Step = 11;
                        break;
                    case 13:
                        if (sim.S.Points.Exists(p => p.Kind == "machine_m")) Step = 14;
                        else if (!sim.CanBuy("bin_m:1").Ok && sim.CanBuy("bin_m:1").Reason == "cash") Step = 14;
                        break;
                    case 14:
                        if (sim.S.Drivers >= 2 || sim.CanBuy("driver").Reason == "cash") { Finish(sim, true); return; }
                        break;
                }
                if (Step == before) break;
            }
        }

        static bool PetComing(Simulation sim)
        {
            var d = sim.S.Depot;
            if (d.Intake[Mats.Pet] > 0) return true;
            if (d.BalerIn[Mats.Pet] + d.Press[Mats.Pet] >= sim.Gd.Materials[Mats.Pet].BaleKg - 1e-6 && !d.BalerDown) return true;
            foreach (var t in sim.S.Trucks) if (t.Load[Mats.Pet] > 0) return true;
            return false;
        }

        /// <summary>First-time context tips after the guide (each once): the event -> tip key, "" = none.</summary>
        public static string TipFor(SimEvent e, Simulation sim)
        {
            switch (e.Type)
            {
                case "jam": return "tip.jam";
                case "yard_full": return "tip.yard";
                case "baler_down": return "tip.baler_down";
                case "gate_queue": return "tip.gate";
                case "district": return e.Text == "oldtown" ? "tip.oldtown" : e.Text == "university" ? "tip.university" : e.Text == "stadium" ? "tip.stadium" : "";
                case "research_done":
                    return e.Text == "sensors" ? "tip.sensors" : e.Text == "dispatcher" ? "tip.dispatcher" : e.Text == "counting_centre" ? "tip.centre"
                        : e.Text == "horeca" ? "tip.horeca" : e.Text == "shop_points" ? "tip.shops" : "";
                case "crisis":
                    switch (e.Text)
                    {
                        case "heat_wave": return "tip.heat";
                        case "match_day": return "tip.match";
                        case "sensor_offline": return "tip.sensor_offline";
                        case "bulk_returner": return "tip.bulk";
                        case "breakdown": return "tip.breakdown";
                        case "fuel_spike": return "tip.fuel";
                        case "bale_crash": return "tip.market";
                        case "audit": return "tip.audit";
                    }
                    return "";
                case "day_done": return sim.S.Day == 1 ? "tip.report" : "";
            }
            return "";
        }
    }
}
