// Ridgeback Rangers — the 12-step show-and-do guide (plan §9, fleet §9). One thing per step; the target is spotlit; the step
// advances ONLY when the player really does that action; a wrong tap repeats the hint (no penalty); time is paused while
// the guide waits for the player, and runs fast while the guide waits for the world (the Feeder driving the crate, the first
// visitors walking in, the rest of the first day). Safe: the sim is in tutorial mode (no pin ever fails, no island events,
// no animal can leave a paddock, no ranger can be lost). Skip + Settings -> "Replay the guide".
// Volt City lesson (11-01, 1★ "guide error"): every step is recoverable — leaving the screen / relaunching mid-step
// re-derives the step from the career (Sync), never locks the player.
//   1 tap the Chief's badge              7 send the tram out
//   2 pan / zoom the island              8 a squabble: send the Handler
//   3 tap the hungry herd's pin          9 compare two animals (harmony bar)
//   4 send a Feeder                     10 build a steel fence post
//   5 watch the crate arrive -> "Fed!"  11 the day report -> Next day
//   6 open the gate                     12 the hatchery: check an egg's temperature
using System;
using System.Collections.Generic;
using RidgebackRangers.Sim;

namespace RidgebackRangers.Game
{
    public sealed class Coach
    {
        public const int Steps = 12;
        public const int Done = 13;
        /// <summary>The guide's two animals for the Compatibility card (a triceratops and the maiasaura).</summary>
        public const int CompareA = 0, CompareB = 2;
        public const int SquabblePaddock = 1;
        public const string GiftEgg = "maiasaura";

        /// <summary>What the UI spotlights per step (world: pin:&lt;kind&gt; / gate / tram; HUD: badge / camera; sheets: ranger:&lt;role&gt;,
        /// fed, animal:&lt;i&gt;, fence:&lt;p&gt;, next_day, temp:&lt;slot&gt;). Steps 4, 8 and 9 change their target as the player goes.</summary>
        public static readonly string[] Targets =
        {
            "", "badge", "camera", "pin:hungry", "ranger:feeder", "fed", "gate", "tram", "pin:squabble", "animal:" + CompareA, "fence:" + SquabblePaddock, "next_day", "temp:0",
        };

        /// <summary>The action id (prefix) that completes each step.</summary>
        public static readonly string[] Need =
        {
            "", "badge", "camera", "pin:", "send:", "fed_ok", "gate", "tram_go", "send:", "animal:", "buy:fence:" + SquabblePaddock, "next_day", "temp:",
        };

        public int Step { get; private set; }
        public bool Active { get; private set; }
        public int WrongTaps { get; private set; }
        /// <summary>Increments on a wrong tap: the UI pulses the hint again.</summary>
        public int Flash { get; private set; }
        public Action<string, Dictionary<string, object>> Analytics = (n, p) => { };
        public Action<int, bool> Persist = (step, done) => { };
        Simulation sim;
        GameActions actions;

        public string BubbleKey => Active && Step >= 1 && Step <= Steps ? "coach." + Step : "";

        public string Target
        {
            get
            {
                if (!Active || Step < 1 || Step > Steps) return "";
                switch (Step)
                {
                    case 8:
                    {
                        var p = TutorialPin(PinKinds.Squabble);
                        return p != null && actions != null && actions.SelectedPin == p.Id ? "ranger:handler" : "pin:squabble";
                    }
                    case 9: return actions != null && actions.CompareFirst == CompareA ? "animal:" + CompareB : "animal:" + CompareA;
                    case 5: return HungryOpen() ? "" : "fed";
                    case 7: return FastForward(sim) ? "" : "tram";
                    case 11: return sim != null && sim.Phase == Phases.Report ? "next_day" : "";
                }
                return Targets[Step];
            }
        }

        PinState TutorialPin(string kind) => sim?.S.Pins.Find(p => p.Kind == kind);
        bool HungryOpen() => TutorialPin(PinKinds.Hungry) != null;

        /// <summary>True while the guide waits for the world, not the player: the sim runs fast.</summary>
        public bool FastForward(Simulation s)
        {
            if (!Active || s == null) return false;
            switch (Step)
            {
                case 5: return s.S.Pins.Exists(p => p.Kind == PinKinds.Hungry);
                case 7: return s.Phase == Phases.Live && !s.S.Tram.Running && s.Queued == 0 && s.S.Tram.Riders == 0;
                case 11: return s.Phase == Phases.Live;
            }
            return false;
        }

        double FastRate => Step == 5 ? 8.0 : Step == 7 ? 10.0 : 60.0;   // game minutes per real second

        /// <summary>True while the guide waits for the player: the sim is paused.</summary>
        public bool WantsPause(Simulation s) => Active && !FastForward(s);

        public void Begin(Simulation s, GameActions a, int resumeStep = 0)
        {
            sim = s;
            actions = a;
            Active = true;
            Step = Math.Max(1, Math.Min(Steps, resumeStep));
            if (Step == 1) Analytics("onboarding_begin", new Dictionary<string, object>());
            if (s.Phase == Phases.Plan && Step <= 11 && s.S.Day == 1) s.StartDay();
            Sync(s, a);
            Persist(Step, false);
        }

        public void Skip(Simulation s)
        {
            if (!Active) return;
            Analytics("onboarding_skip", new Dictionary<string, object> { { "step", (long)Step } });
            Finish(s, false);
        }

        void Finish(Simulation s, bool completed)
        {
            Active = false;
            s.EndTutorial();
            s.SetPaused(false);
            if (completed) Analytics("onboarding_complete", new Dictionary<string, object> { { "wrong_taps", (long)WrongTaps } });
            Step = Done;
            Persist(Done, true);
        }

        static bool Harmless(string action) => action == "open" || action == "pause" || action == "resume" || action == "camera" || action == "clear";

        /// <summary>GameActions.Allowed: during the guide only the step's action (and harmless UI) may run.</summary>
        public bool Allows(string action)
        {
            if (!Active) return true;
            if (Harmless(action)) return true;
            if ((Step == 4 || Step == 8) && action.StartsWith("pin:", StringComparison.Ordinal)) return true;   // (re)select the pin
            string need = Step >= 1 && Step <= Steps ? Need[Step] : "";
            return need != "" && action.StartsWith(need, StringComparison.Ordinal);
        }

        bool Matches(Simulation s, GameActions a, string action)
        {
            string need = Step >= 1 && Step <= Steps ? Need[Step] : "";
            if (need == "" || action == null || !action.StartsWith(need, StringComparison.Ordinal)) return false;
            var parts = action.Split(':');
            switch (Step)
            {
                case 3:
                {
                    var p = parts.Length > 1 && int.TryParse(parts[1], out int id) ? s.Pin(id) : null;
                    return p != null && p.Kind == PinKinds.Hungry;
                }
                case 4:
                case 8:
                {
                    // send:<pin>:<ranger> ran OK = the right role was sent (the sim checks the role); step 8 = the squabble
                    var p = parts.Length > 1 && int.TryParse(parts[1], out int id) ? s.Pin(id) : null;
                    return p != null && p.Kind == (Step == 4 ? PinKinds.Hungry : PinKinds.Squabble);
                }
                case 9: return a != null && a.CompareFirst >= 0 && a.CompareSecond >= 0;
            }
            return true;
        }

        /// <summary>A tap the guide did not ask for: hint again, no penalty.</summary>
        public void WrongTap()
        {
            if (!Active) return;
            WrongTaps++;
            Flash++;
        }

        /// <summary>GameActions.Ran listener.</summary>
        public void OnAction(Simulation s, GameActions a, string id, ActionResult r)
        {
            if (!Active) return;
            sim = s;
            actions = a;
            if (r.Error == "blocked") { WrongTap(); return; }
            if (!r.Ok) { if (Step == 4 || Step == 8) WrongTap(); return; }   // e.g. sent the wrong role: hint again
            if (!Matches(s, a, id)) return;
            Advance(s, a);
        }

        void Advance(Simulation s, GameActions a)
        {
            Analytics("onboarding_step", new Dictionary<string, object> { { "step", (long)Step } });
            Step++;
            if (Step > Steps) { Finish(s, true); return; }
            Sync(s, a);
            Persist(Step, false);
        }

        /// <summary>Every frame: runs the world while the guide waits for it, keeps the first day alive.</summary>
        public void Drive(Simulation s, GameActions a, double realDt)
        {
            if (!Active) return;
            sim = s;
            actions = a;
            if (s.Phase == Phases.Plan && Step <= 11 && s.S.Day == 1) s.StartDay();
            if (s.Phase == Phases.Report && Step < 11) Step = 11;   // the day ended under the guide (relaunch): straight to the report step
            Sync(s, a);
            if (!Active) return;
            if (FastForward(s))
            {
                s.SetPaused(false);
                s.AdvanceMinutes(Math.Max(0, Math.Min(0.25, realDt)) * FastRate);
            }
            else s.SetPaused(true);
        }

        /// <summary>Recovery: re-derives a step whose preconditions are gone (relaunch, the screen was left, the day moved
        /// on) and puts the guide's pins / egg in place. Never sends the player forward past an action they still have to
        /// learn, never locks them.</summary>
        public void Sync(Simulation s, GameActions a)
        {
            if (!Active) return;
            sim = s;
            actions = a;
            int guard = 0;
            while (guard++ < Steps)
            {
                int before = Step;
                switch (Step)
                {
                    case 3:
                        if (s.Phase == Phases.Live && TutorialPin(PinKinds.Hungry) == null) s.ForcePin(PinKinds.Hungry, 0);
                        break;
                    case 4:
                    {
                        var p = TutorialPin(PinKinds.Hungry);
                        if (p == null || p.Ranger >= 0) Step = 5;                       // already sent / already fed (relaunch)
                        else if (a == null || a.SelectedPin != p.Id) Step = 3;          // the selection is UI state: tap the pin again
                        break;
                    }
                    case 6:
                        if (s.S.GateOpen) Step = 7;
                        break;
                    case 7:
                        if (s.S.Tram.Running) Step = 8;
                        break;
                    case 8:
                    {
                        if (s.Phase != Phases.Live) break;
                        var p = TutorialPin(PinKinds.Squabble);
                        if (p == null) s.ForcePin(PinKinds.Squabble, SquabblePaddock);
                        else if (p.Ranger >= 0) Step = 9;
                        break;
                    }
                    case 10:
                        if (s.S.Paddocks.Count > SquabblePaddock && s.S.Paddocks[SquabblePaddock].Fence >= 2) Step = 11;
                        else if (s.CanBuy("fence:" + SquabblePaddock).Reason == "cash") Step = 11;
                        break;
                    case 11:
                        if (s.Phase == Phases.Plan && s.S.Day > 1) Step = 12;
                        break;
                    case 12:
                        if (s.S.Eggs.Count == 0) s.PlaceEgg(GiftEgg);
                        if (s.S.Eggs.Count > 0 && s.S.Eggs[0].Checked) { Finish(s, true); return; }
                        break;
                }
                if (Step == before) break;
            }
        }

        /// <summary>First-time context tips after the guide (each once): the event -> tip key, "" = none.</summary>
        public static string TipFor(SimEvent e, Simulation s)
        {
            switch (e.Type)
            {
                case "pin":
                    switch (e.Text)
                    {
                        case PinKinds.Sick: return "tip.vet";
                        case PinKinds.Fence: return "tip.fence";
                        case PinKinds.Escape: return "tip.escape";
                        case PinKinds.Flood: return "tip.flood";
                        case PinKinds.Squabble: return "tip.squabble";
                    }
                    return "";
                case "zone": return e.Text == "fern" ? "tip.fern" : e.Text == "dune" ? "tip.dune" : "";
                case "egg": return "tip.egg";
                case "egg_cold": return "tip.egg_cold";
                case "hatched": return e.B < 0 ? "tip.nursery" : "tip.hatched";
                case "dig_start": return "tip.dig";
                case "pin_failed": return "tip.failed";
                case "crisis":
                    switch (e.Text)
                    {
                        case "heat_wave": return "tip.heat";
                        case "school_trip": return "tip.school";
                        case "supply_late": return "tip.supply";
                        case "dust_storm": return "tip.dust";
                        case "rain_squall": return "tip.rain";
                    }
                    return "";
                case "day_done": return s.S.Day <= 2 ? "tip.report" : "";
            }
            return "";
        }
    }
}
