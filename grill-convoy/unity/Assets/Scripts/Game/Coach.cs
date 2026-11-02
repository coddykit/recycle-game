// Grill Convoy — the 14-step show-and-do guide (design §9, fleet §9). One thing per step; the target is spotlit; the step
// advances ONLY when the player really does that action; a wrong tap repeats the hint (no penalty); time is paused while
// the guide waits for the player, and runs fast while the guide waits for the world (the patty on the grill, the rest of
// the first day, the morning until the lunch rush). Safe: the sim is in tutorial mode (no crises, no Fame loss).
// Skip + Settings -> "Replay the guide".
// Every step is recoverable (Volt City lesson 11-01): leaving the screen / relaunching mid-step re-derives the step from
// the career (Sync) — the hand-cooked ticket's stage is saved in the career — never locks the player; a step whose action
// is no longer possible (no cash for the hire, a full fridge) is passed, never blocked (reference game's hard-lock review).
//   1 open the window                  8 hire a runner
//   2 tap the ticket                   9 move a station in the layout (toaster next to the bench)
//   3 tap the grill (patty on)        10 commissary: prep patties ×60
//   4 flip (after a beat)             11 pack 20 more patties on the truck
//   5 assemble (bun, cheese, sauce)   12 start the day (the truck drives to its pitch)
//   6 serve at the window             13 lunch rush: All hands
//   7 day report -> Next day          14 flare-up: Extinguish
using System;
using System.Collections.Generic;
using GrillConvoy.Sim;

namespace GrillConvoy.Game
{
    public sealed class Coach
    {
        public const int Steps = 14;
        public const int Done = 15;
        public const double LunchMinute = 720;

        /// <summary>What the UI spotlights per step (HUD: main / cook / allhands / extinguish; sheets: next_day, hire,
        /// layout, prep_patty, load_patty).</summary>
        public static readonly string[] Targets =
        {
            "", "main", "cook", "cook", "cook", "cook", "cook", "next_day", "hire", "layout", "prep_patty", "load_patty", "main", "allhands", "extinguish",
        };

        /// <summary>The action id (prefix) that completes each step.</summary>
        public static readonly string[] Need =
        {
            "", "start_day", "ticket", "cook:grill", "cook:flip", "cook:assemble", "cook:serve", "next_day", "buy:hire:0:runner", "move:0:",
            "prep:patty", "load:0:patty:", "start_day", "allhands:0", "extinguish:0",
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
            if (Step == 4) return sim.S.HandStage == 3;
            if (Step == 7) return sim.Phase == Phases.Live;
            if (Step == 13)
            {
                var t = sim.S.Trucks.Count > 0 ? sim.S.Trucks[0] : null;
                return sim.Phase == Phases.Live && (t == null || !t.Open || sim.S.Minute < LunchMinute);
            }
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
            if (Active) Persist(Step, false);
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

        /// <summary>GameActions.Allowed: during the guide only the step's action (and harmless UI navigation) may run.</summary>
        public bool Allows(string action)
        {
            if (!Active) return true;
            if (action == "open" || action == "pause" || action == "resume" || action.StartsWith("select_", StringComparison.Ordinal)) return true;
            return Matches(action);
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
            if (Step == 1) sim.BeginHandTicket();
            Advance(sim, actions);
        }

        void Advance(Simulation sim, GameActions actions)
        {
            Analytics("onboarding_step", new Dictionary<string, object> { { "step", (long)Step } });
            Step++;
            if (Step > Steps) { Finish(sim, true); return; }
            Sync(sim, actions);
            if (Active) Persist(Step, false);
        }

        /// <summary>Every frame: runs the world while the guide waits for it, keeps the steps in sync.</summary>
        public void Drive(Simulation sim, GameActions actions, double realDt)
        {
            if (!Active) return;
            if (FastForward(sim))
            {
                sim.SetPaused(false);
                double minutes = Math.Max(0, realDt) * 40.0;   // ~100 game minutes per real second
                if (Step == 13) minutes = Math.Min(minutes, Math.Max(0.5, LunchMinute - sim.S.Minute));
                sim.AdvanceMinutes(minutes);
            }
            else sim.SetPaused(true);
            Sync(sim, actions);
        }

        /// <summary>Recovery: re-derives a step whose preconditions are gone (relaunch, the screen was left, the day moved
        /// on). Never sends the player forward past an action they still have to learn, never locks them.</summary>
        public void Sync(Simulation sim, GameActions actions)
        {
            if (!Active) return;
            var t0 = sim.S.Trucks.Count > 0 ? sim.S.Trucks[0] : null;
            int guard = 0;
            while (guard++ < Steps + 2)
            {
                int before = Step;
                switch (Step)
                {
                    case 1:
                        if (sim.Phase == Phases.Live && sim.S.Day == 1) Step = sim.S.HandStage > 0 ? HandStep(sim.S.HandStage) : 7;
                        else if (sim.S.Day > 1 || sim.Phase == Phases.Report) Step = 7;
                        break;
                    case 2: case 3: case 4: case 5: case 6:
                        if (sim.Phase == Phases.Plan && sim.S.Day == 1) Step = 1;
                        else if (sim.S.HandStage > 0) Step = HandStep(sim.S.HandStage);
                        else Step = 7;   // the ticket is served (or the day moved on): the crew takes over
                        break;
                    case 7:
                        if (sim.Phase == Phases.Plan && sim.S.Day > 1) Step = 8;
                        break;
                    case 8:
                        if (t0 == null || t0.Crew.Contains(Roles.Runner) || !sim.CanBuy("hire:0:runner").Ok) Step = 9;
                        break;
                    case 9:
                        if (t0 == null || t0.Layout.Count < 2) Step = 10;
                        break;
                    case 10:
                        if (!CanPrepPatty(sim)) Step = 11;
                        break;
                    case 11:
                        if (t0 == null || sim.Phase != Phases.Plan || !sim.AtCommissary(t0) || sim.S.Commissary.Stock[Stock.Patty] <= 0 || t0.Load[Stock.Patty] >= sim.TruckCap(t0, Stock.Patty)) Step = 12;
                        break;
                    case 12:
                        if (sim.Phase == Phases.Live) Step = 13;
                        else if (t0 == null || t0.Pitch < 0) Step = 13;
                        break;
                    case 13:
                        if (sim.Phase != Phases.Live || t0 == null) { Finish(sim, true); return; }
                        if (t0.AllHandsUntil > sim.S.Minute) Step = 14;
                        break;
                    case 14:
                        if (sim.Phase != Phases.Live || t0 == null || !t0.Open) { Finish(sim, true); return; }
                        if (!t0.Flaring) sim.Flare(0);
                        break;
                }
                if (Step == before) break;
            }
        }

        static int HandStep(int stage) => stage switch { 1 => 2, 2 => 3, 3 => 4, 4 => 4, 5 => 5, _ => 6 };

        static bool CanPrepPatty(Simulation sim)
        {
            var c = sim.S.Commissary;
            int item = Stock.Patty;
            return c.Queue.Count < Simulation.MaxPrepQueue && sim.S.Cash >= sim.BatchCost(item)
                && c.Stock[item] + sim.Queued(item) + sim.BatchSize(item) <= sim.Gd.StockDefs[item].Cap && sim.Phase != Phases.Report;
        }

        /// <summary>First-time context tips after the guide (each once): the event -> tip key, "" = none.</summary>
        public static string TipFor(SimEvent e, Simulation sim)
        {
            switch (e.Type)
            {
                case "sold_out": return "tip.sold_out";
                case "walked": return e.Text == "line" ? "tip.line" : "";
                case "propane_low": return "tip.propane";
                case "truck_new": return "tip.truck2";
                case "district": return e.Text == "boardwalk" ? "tip.boardwalk" : e.Text == "stadium" ? "tip.stadium" : "";
                case "research_done":
                    switch (e.Text)
                    {
                        case "fryer": return "tip.fryer";
                        case "drink_fridge": return "tip.fridge";
                        case "order_ahead": return "tip.order_ahead";
                        case "second_window": return "tip.second_window";
                        case "dispatcher": return "tip.dispatcher";
                        case "par_levels": return "tip.par";
                        case "premium_tier": return "tip.premium";
                    }
                    return "";
                case "crisis":
                    switch (e.Text)
                    {
                        case "halftime": return "tip.halftime";
                        case "heat": return "tip.heat";
                        case "rain": return "tip.rain";
                        case "propane": return "tip.propane_out";
                        case "flare": return "tip.flare";
                        case "rival": return "tip.rival";
                    }
                    return "";
                case "day_done": return sim.S.Day == 1 ? "tip.report" : "";
                case "day_start": return sim.MatchDay(sim.S.Day + 1) && sim.PitchOpen(sim.Gd.Pitches.Find(n => sim.Gd.Nodes[n].Match)) ? "tip.match_tomorrow" : "";
            }
            return "";
        }
    }
}
