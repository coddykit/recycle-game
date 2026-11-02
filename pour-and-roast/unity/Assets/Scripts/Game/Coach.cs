// Pour & Roast — the show-and-do guide (design §9, fleet §9). One thing per step; the target is spotlit; the step advances
// ONLY when the player really does that action; a wrong tap repeats the hint (no penalty); time is paused while the guide
// waits for the player, and runs fast while the guide waits for the world (the shot being pulled, the latte, the batch,
// the pre-order). Safe: the sim is in tutorial mode (no random customers, no walk-outs, no crises, no rating loss). The
// guide places its own orders. Skip + Settings -> "How to play" replays it.
// Volt City lesson (11-01, 1★ "guide error"): every step is recoverable — leaving the screen / relaunching mid-step
// re-derives the step from the career (Sync), never locks the player.
//    1 Open the cart                       7 two same drinks: turn on Batch
//    2 tap the first ticket on the rail    8 a pre-order with a pickup time: tap Priority
//    3 tap the espresso machine (brew)     9 the shots taste bitter: hold the grinder (dial-in)
//    4 tap the pickup shelf (hand over)   10 buy a table (layout)
//    5 a latte: tap the milk fridge       11 rain tomorrow: prep hot drinks (forecast strip)
//    6 hire a second barista (steam wand) 12 set the house rule: deadline first
// The plan's steps 12-13 (the roastery cards, the Settings replay) are the first-time roastery tips (TipFor: market ->
// roast -> rest -> blend, shown when the Roastery Hall opens) and Settings -> "How to play".
using System;
using System.Collections.Generic;
using PourAndRoast.Sim;

namespace PourAndRoast.Game
{
    public sealed class Coach
    {
        public const int Steps = 12;
        public const int Done = 13;
        public const double FastMinPerRealS = 40.0;

        /// <summary>What the UI spotlights per step (HUD: open / batch; rail: ticket:first; world markers: station:espresso /
        /// shelf / fridge / grinder; ticket bar: flag; sheets: hire:barista / buy:seats / prep:hot / rule:deadline).</summary>
        public static readonly string[] Targets =
        {
            "", "open", "ticket:first", "station:espresso", "shelf", "fridge", "hire:barista", "batch", "flag", "grinder", "buy:seats", "prep:hot", "rule:deadline",
        };

        /// <summary>The action id (prefix) that completes each step.</summary>
        public static readonly string[] Need =
        {
            "", "start_day", "select_ticket:", "brew", "handover", "fridge", "buy:staff:barista", "batch:on", "flag:", "dial_in", "buy:seats", "prep:hot", "rule:deadline",
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

        static bool IsMilk(Simulation sim, TicketState t)
        {
            foreach (var it in t.Items) { var d = sim.ItemDef(it); if (d != null && d.Milk) return true; }
            return false;
        }

        /// <summary>True while the guide waits for the world, not the player: the sim runs fast.</summary>
        public bool FastForward(Simulation sim)
        {
            if (!Active || sim.Phase != Phases.Live) return false;
            var tk = sim.S.Tickets;
            switch (Step)
            {
                case 4: return tk.Exists(t => !t.Ready);                         // the first shot is being pulled
                case 7: return tk.Exists(t => IsMilk(sim, t));                  // the new barista steams the latte
                case 8: return tk.Exists(t => !t.Pre);                          // the batched americanos go out
                case 9: return tk.Count > 0;                                    // the flagged pre-order is picked up
            }
            return false;
        }

        /// <summary>True while the guide waits for the player: the sim is paused.</summary>
        public bool WantsPause(Simulation sim) => Active && !FastForward(sim);

        public void Begin(Simulation sim, GameActions actions, int resumeStep = 0)
        {
            Active = true;
            sim.S.Tutorial = true;   // safe mode (also on a replay from Settings): no random customers, no walk-outs, no crises
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
            if (action == "open" || action == "pause" || action == "resume" || action == "deselect") return true;
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

        /// <summary>Every frame: runs the world while the guide waits for it, keeps the day alive, places the guide's orders.</summary>
        public void Drive(Simulation sim, GameActions actions, double realDt)
        {
            if (!Active) return;
            sim.HoldShelf = Step <= 4;
            if (sim.Phase == Phases.Report) sim.NextDay();
            if (sim.Phase == Phases.Plan && Step >= 2) sim.StartDay();
            if (FastForward(sim))
            {
                sim.SetPaused(false);
                sim.AdvanceMinutes(Math.Max(0, Math.Min(0.25, realDt)) * FastMinPerRealS);
            }
            else sim.SetPaused(true);
            Sync(sim, actions);
        }

        /// <summary>Recovery + the guide's orders: re-derives a step whose preconditions are gone (relaunch, the screen was
        /// left, the day moved on). Never sends the player forward past an action they still have to learn, never locks them.</summary>
        public void Sync(Simulation sim, GameActions actions)
        {
            if (!Active) return;
            sim.HoldShelf = Step <= 4;
            var s = sim.S;
            int guard = 0;
            while (guard++ < Steps + 2)
            {
                int before = Step;
                bool live = sim.Phase == Phases.Live;
                switch (Step)
                {
                    case 1:
                        if (sim.Phase != Phases.Plan) Step = 2;
                        break;
                    case 2:
                        if (live && s.Tickets.Count == 0)
                        {
                            if (s.Today.Served > 0) Step = 5;                  // relaunch after the first hand-over
                            else sim.TutorialOrder("espresso");
                        }
                        break;
                    case 3:
                    {
                        var t = actions?.Selected;
                        if (t != null && (t.Items[0].Worker != -1 || t.Items[0].Stage > 0 || t.Ready)) Step = 4;
                        else if (s.Tickets.Exists(x => x.Items[0].Worker != -1 || x.Items[0].Stage > 0 || x.Ready)) Step = 4;
                        else if (t == null) Step = 2;                          // the selection is UI state: pick the ticket again
                        break;
                    }
                    case 4:
                        if (s.Tickets.Count == 0 && s.Today.Served > 0) Step = 5;
                        else if (s.Tickets.Count == 0) Step = 2;
                        break;
                    case 5:
                        if (live && !s.Tickets.Exists(t => IsMilk(sim, t)) && sim.CountRole(Roles.Barista) < 2) sim.TutorialOrder("latte");
                        break;
                    case 6:
                        if (sim.CountRole(Roles.Barista, s.Venue) >= 2 || sim.CanBuy("staff:barista").Reason == "cash") Step = 7;
                        break;
                    case 7:
                        if (s.Batch) Step = 8;
                        else if (live && s.Tickets.Count == 0) { sim.TutorialOrder("americano"); sim.TutorialOrder("americano"); }
                        break;
                    case 8:
                    {
                        var pre = s.Tickets.Find(t => t.Pre);
                        if (pre != null && pre.Flag) { Step = 9; break; }
                        if (live && s.Tickets.Count == 0) pre = sim.TutorialOrder("preorder");
                        if (pre != null && actions != null && actions.SelectedTicket != pre.Id && !s.Tickets.Exists(t => !t.Pre)) actions.Select(pre.Id);
                        break;
                    }
                    case 9:
                        if (s.Tickets.Count == 0 && sim.Venue != null && sim.Venue.Tune > sim.Gd.TuneOff - 1) sim.Venue.Tune = 40;   // the grind has drifted
                        break;
                    case 10:
                        if (sim.Venue != null && sim.Venue.Seats >= 1 || sim.CanBuy("seats").Reason == "cash" || sim.CanBuy("seats").Reason == "max") Step = 11;
                        break;
                    case 11:
                        if (s.PrepHot > 0 || s.Cash < sim.Gd.PrepHotPrice) Step = 12;
                        break;
                    case 12:
                        if (s.Rule == Rules.Deadline) { Finish(sim, true); return; }
                        break;
                }
                if (Step == before) break;
            }
        }

        /// <summary>First-time context tips after the guide (each once): the event -> tip key, "" = none. The roastery cards
        /// (market -> roast -> rest -> blend) are the plan's onboarding step 12.</summary>
        public static string TipFor(SimEvent e, Simulation sim)
        {
            switch (e.Type)
            {
                case "venue": return e.Text == "corner" ? "tip.corner" : e.Text == "roastery" ? "tip.roastery" : "";
                case "green_bought": return "tip.roast";
                case "roast_done": return "tip.rest";
                case "day_start": return sim.ReadyToBlendKg > 0 ? "tip.blend" : sim.S.Day == 2 ? "tip.plan" : "";
                case "balk": return "tip.rail_full";
                case "walked": return "tip.walked";
                case "item_failed":
                    return e.Text == "milk" ? "tip.milk_out" : e.Text == "beans" ? "tip.beans_out" : e.Text == "ice" ? "tip.ice_out" : e.Text == "sold_out" ? "tip.sold_out" : "";
                case "research_done":
                    return e.Text == "head_barista" ? "tip.head_barista" : e.Text == "roast_assistant" ? "tip.assistant" : e.Text == "forecast_board" ? "tip.forecast" : "";
                case "crisis":
                    switch (e.Text)
                    {
                        case "rain": return "tip.rain";
                        case "heat": return "tip.heat";
                        case "milk_late": return "tip.milk_late";
                        case "grinder_jam": return "tip.jam";
                        case "frost": return "tip.frost";
                        case "review_spree": return "tip.spree";
                        case "wholesale_short": return "tip.wholesale";
                    }
                    return "";
                case "day_done": return sim.S.Day == 1 ? "tip.report" : "";
            }
            return "";
        }
    }
}
