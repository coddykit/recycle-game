// Charge Nurse — the 10-step show-and-do guide (plan §9, fleet §9). One thing per step; the target is spotlit; the step
// advances ONLY when the player really does that action; a wrong tap repeats the hint (a wrong colour explains why); time
// is paused while the guide waits for the player and runs fast while it waits for the world (the treatment, the shift
// running to the handover window). Safe: the sim is in tutorial mode (nobody worsens or leaves, no crises). Skip +
// Settings → "Replay the guide". Every step is recoverable: leaving the screen / relaunching mid-step re-derives the step
// from the career (Sync), never locks the player (Volt City lesson, 11-01).
//   1 tap the waiting patient          6 an ambulance: Accept (red goes first)
//   2 tap Green (mild)                 7 open the X-ray room
//   3 send them to the free bay        8 hire a second porter
//   4 (treated) tap Discharge          9 tap the red line: the bottleneck lens (discharge)
//   5 tap the dirty bed to clean it   10 the shift is ending: tap Handover
using System;
using System.Collections.Generic;
using ChargeNurse.Sim;

namespace ChargeNurse.Game
{
    public sealed class Coach
    {
        public const int Steps = 10;
        public const int Done = 11;
        /// <summary>The guide's first patient (the career's patient #1).</summary>
        public const int FirstPatient = 1;

        /// <summary>What the UI spotlights per step (world markers: patient:1 / bay:free / discharge:1 / clean; sheet pills:
        /// tag:0 / room:xray / hire:porter; HUD: ambulance / lens / handover).</summary>
        public static readonly string[] Targets =
        {
            "", "patient:1", "tag:0", "bay:free", "discharge:1", "clean", "ambulance", "room:xray", "hire:porter", "lens", "handover",
        };

        /// <summary>The action id (prefix) that completes each step.</summary>
        public static readonly string[] Need =
        {
            "", "select_patient:1", "tag:1:0", "assign:", "discharge:1", "clean:", "ambulance:accept", "buy:room:xray", "buy:hire:porter", "lens", "handover",
        };

        public int Step { get; private set; }
        public bool Active { get; private set; }
        public int WrongTaps { get; private set; }
        /// <summary>Increments on a wrong tap: the UI pulses the hint again.</summary>
        public int Flash { get; private set; }
        /// <summary>A step-specific "not quite" hint after a wrong tap ("" = the step's own text).</summary>
        public string WrongKey { get; private set; } = "";
        public Action<string, Dictionary<string, object>> Analytics = (n, p) => { };
        public Action<int, bool> Persist = (step, done) => { };
        public string Target => Active && Step >= 1 && Step <= Steps ? Targets[Step] : "";
        public string BubbleKey => !Active || Step < 1 || Step > Steps ? "" : WrongKey != "" ? WrongKey : "coach." + Step;

        /// <summary>True while the guide waits for the world, not the player: the sim runs fast.</summary>
        public bool FastForward(Simulation sim)
        {
            if (!Active || sim.Phase != Phases.Live) return false;
            if (Step == 4) { var p = sim.P(FirstPatient); return p != null && p.Stage != Stage.Ready; }
            if (Step == 10) return sim.S.Minute < sim.Gd.HandoverFrom;
            return false;
        }

        /// <summary>True while the guide waits for the player: the sim is paused.</summary>
        public bool WantsPause(Simulation sim) => Active && !FastForward(sim);

        public void Begin(Simulation sim, GameActions actions, int resumeStep = 0)
        {
            Active = true;
            WrongKey = "";
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
            WrongKey = "";
            Persist(Done, true);
        }

        /// <summary>GameActions.Allowed: during the guide only the step's action (and harmless UI) may run.</summary>
        public bool Allows(string action)
        {
            if (!Active) return true;
            if (action == "open" || action == "pause" || action == "resume") return true;
            return Matches(action);
        }

        public bool Matches(string action)
        {
            string need = Step >= 1 && Step <= Steps ? Need[Step] : "";
            return need != "" && action != null && action.StartsWith(need, StringComparison.Ordinal);
        }

        /// <summary>A tap the guide did not ask for: hint again, no penalty.</summary>
        public void WrongTap(string action = "")
        {
            if (!Active) return;
            WrongTaps++;
            Flash++;
            WrongKey = Step == 2 && action != null && action.StartsWith("tag:", StringComparison.Ordinal) ? "coach.2.wrong" : "";
        }

        /// <summary>GameActions.Ran listener.</summary>
        public void OnAction(Simulation sim, GameActions actions, string id, ActionResult r)
        {
            if (!Active) return;
            if (r.Error == "blocked") { WrongTap(id); return; }
            if (!r.Ok || !Matches(id)) return;
            if (Step == 7) sim.StartArrivals();     // walk-ins start once the X-ray room is open
            if (Step == 9) sim.ReleaseGuided();     // the nurses take the two discharges now
            Advance(sim, actions);
        }

        void Advance(Simulation sim, GameActions actions)
        {
            Analytics("onboarding_step", new Dictionary<string, object> { { "step", (long)Step } });
            Step++;
            WrongKey = "";
            if (Step > Steps || sim.Phase == Phases.Report) { Finish(sim, true); return; }
            Sync(sim, actions);
            if (Active) Persist(Step, false);
        }

        /// <summary>Every frame: runs the world while the guide waits for it, keeps the shift alive.</summary>
        public void Drive(Simulation sim, GameActions actions, double realDt)
        {
            if (!Active) return;
            if (sim.Phase == Phases.Plan && Step >= 4) sim.StartDay();
            if (FastForward(sim))
            {
                sim.SetPaused(false);
                sim.AdvanceMinutes(Math.Max(0, realDt) * 40.0);   // ~120 game minutes per real second
            }
            else sim.SetPaused(true);
            Sync(sim, actions);
        }

        /// <summary>Recovery: re-derives a step whose preconditions are gone (relaunch, the screen was left, the day moved
        /// on). Never sends the player forward past an action they still have to learn, never locks them.</summary>
        public void Sync(Simulation sim, GameActions actions)
        {
            if (!Active) return;
            var p1 = sim.P(FirstPatient);
            bool selected = actions != null && actions.SelectedPatient == FirstPatient;
            int guard = 0;
            while (guard++ < Steps * 2 && Active)
            {
                int before = Step;
                switch (Step)
                {
                    case 1:
                        if (p1 == null) Step = 5;
                        else if (p1.Stage == Stage.Treat || p1.Stage == Stage.Ready || p1.Stage == Stage.Discharge) Step = 4;
                        break;
                    case 2:
                        if (p1 == null || p1.Seen || p1.Stage == Stage.Treat) Step = 1;
                        else if (!selected) Step = 1;
                        else if (p1.Tag == Sev.Green && p1.Stage == Stage.Bay) Step = 3;
                        break;
                    case 3:
                        if (p1 == null || p1.Stage == Stage.Treat || p1.Stage == Stage.Ready) Step = p1 == null ? 5 : 4;
                        else if (!selected || p1.Tag < 0) Step = 1;
                        break;
                    case 4:
                        if (p1 == null) Step = 5;
                        break;
                    case 5:
                        if (!sim.S.Beds.Exists(b => b.Guided && (b.State == BedState.Dirty || b.State == BedState.Cleaning))) Step = 6;
                        break;
                    case 6:
                        if (sim.S.Ambulance == null)
                        {
                            if (sim.S.Today.Ambulances > 0 || sim.Built("xray") || sim.S.Day > 1) Step = 7;
                            else sim.GuideAmbulance();
                        }
                        break;
                    case 7:
                        if (sim.Built("xray")) { sim.StartArrivals(); Step = 8; }
                        break;
                    case 8:
                    {
                        var c = sim.CanBuy("hire:porter");
                        if (sim.Team(Teams.Porter) >= 2 || c.Reason == "cash" || c.Reason == "max") Step = 9;
                        break;
                    }
                    case 9:
                        sim.GuideDischargeSetup();
                        break;
                    case 10:
                        if (sim.Phase == Phases.Report) { Finish(sim, true); return; }
                        break;
                }
                if (Step == before) break;
            }
        }

        /// <summary>First-time context tips after the guide (each once): the event -> tip key, "" = none.</summary>
        public static string TipFor(SimEvent e, Simulation sim)
        {
            switch (e.Type)
            {
                case "ambulance_call": return "tip.ambulance";
                case "worsened": return "tip.worsened";
                case "lwbs": return "tip.lwbs";
                case "transfer": return "tip.transfer";
                case "notes_lost": return "tip.rushed";
                case "treated": return e.Text == Stage.Board ? "tip.board" : "";
                case "wing": return e.Text == "town" ? "tip.town" : e.Text == "city" ? "tip.city" : "";
                case "room": return e.Text == "or" ? "tip.or" : e.Text == "lab" ? "tip.lab" : e.Text == "ct" ? "tip.ct" : "";
                case "research_done":
                    return e.Text == "discharge_lounge" ? "tip.lounge" : e.Text == "fast_track" ? "tip.fast_track" : e.Text == "diversion_rules" ? "tip.diversion"
                        : e.Text == "specialist_track" ? "tip.specialist" : "";
                case "crisis":
                    switch (e.Text)
                    {
                        case "flu_surge": return "tip.surge";
                        case "crash": return "tip.crash";
                        case "power_outage": return "tip.power";
                        case "scanner_down": return "tip.scanner";
                        case "sick_calls": return "tip.sick";
                        case "norovirus": return "tip.norovirus";
                        case "supply_delay": return "tip.supply";
                        case "inspection": return "tip.inspection";
                    }
                    return "";
                case "day_done": return sim.S.Day == 1 ? "tip.report" : "";
            }
            return "";
        }
    }
}
