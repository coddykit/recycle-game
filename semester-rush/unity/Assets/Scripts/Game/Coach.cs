// Semester Rush — the 11-step show-and-do guide (plan section 9, fleet §9). One thing per step; the target is spotlit;
// the step advances ONLY when the player really does that action; a wrong tap repeats the hint (no penalty); time is
// paused while the guide waits for the player, and runs (fast) while it waits for the world (the first bell change, the
// first class, the rest of the week). Safe: the sim is in tutorial mode (no crises, prestige can only rise). Skip +
// Settings -> "Replay the guide". Every step is recoverable: leaving the screen / relaunching mid-step re-derives the step
// from the career (Sync), never locks the player.
//   1 tap the lecture hall                 7 open the gate (a shortcut; late counter drops)
//   2 tap Monday 9:00 on the board          8 collect the tuition (coins)
//   3 pick Business 101 for it              9 build the library ("builders work together")
//   4 give it a professor (Prof. Lee)      10 lunch: move a class to 12:00 (canteen too small)
//   5 Start the week                       11 end of the week: open the grades (prestige rises)
//   6 watch the bell change (no tap; the world runs)
using System;
using System.Collections.Generic;
using SemesterRush.Sim;

namespace SemesterRush.Game
{
    public sealed class Coach
    {
        public const int Steps = 11;
        public const int Done = 12;

        /// <summary>What the UI spotlights per step ({hall} {guide} {gate} {lib} {fix} filled from the career). World markers:
        /// hall:n / gate:e / build:site; HUD: start_week / late / coins / report; sheets: cell / course / prof / move pills.</summary>
        public static readonly string[] Targets =
        {
            "", "hall:{hall}", "cell:0:0", "course:{guide}", "prof:{guide}:0", "start_week", "late", "gate:{gate}", "coins", "build:{lib}", "move:{fix}", "report",
        };

        /// <summary>The action id (prefix) that completes each step.</summary>
        public static readonly string[] Need =
        {
            "", "hall:{hall}", "cell:0:0", "course:{guide}", "prof:{guide}:", "start_week", "", "gate:{gate}", "till", "buy:build:{lib}", "move:", "report",
        };

        /// <summary>UI-only actions that never change the career: always allowed (they open info cards).</summary>
        static readonly string[] Harmless = { "open", "pause", "resume", "building:", "site:", "coins_info", "clear_sel" };

        public int Step { get; private set; }
        public bool Active { get; private set; }
        public int WrongTaps { get; private set; }
        /// <summary>Increments on a wrong tap: the UI pulses the hint again.</summary>
        public int Flash { get; private set; }
        public Action<string, Dictionary<string, object>> Analytics = (n, p) => { };
        public Action<int, bool> Persist = (step, done) => { };
        Simulation sim;

        public string Target => Active && Step >= 1 && Step <= Steps ? Fill(Targets[Step]) : "";
        public string BubbleKey => Active && Step >= 1 && Step <= Steps ? "coach." + Step : "";

        public static int GuideGate(Simulation s)
        {
            foreach (var e in s.Gd.Edges) if (e.Type == "gate" && (e.A == s.Gd.EntranceNode || e.B == s.Gd.EntranceNode)) return e.Index;
            return -1;
        }

        public static string LibrarySite(Simulation s)
        {
            foreach (var n in s.Gd.Nodes) if (n.Kind == "site" && n.Building == "library" && s.Unlocked(n.Zone)) return n.Id;
            return "";
        }

        string Fill(string t)
        {
            if (sim == null || t.IndexOf('{') < 0) return t;
            var fix = t.Contains("{fix}") ? sim.FindLunchFix() : null;
            return t.Replace("{hall}", sim.MainHall().ToString()).Replace("{guide}", sim.Gd.CourseIndex(sim.Gd.GuideCourse).ToString())
                .Replace("{gate}", GuideGate(sim).ToString()).Replace("{lib}", LibrarySite(sim)).Replace("{fix}", fix != null ? fix.Placement.ToString() : "-1");
        }

        /// <summary>The lunch step's move (null when no day ahead has a lunch crunch).</summary>
        public LunchFix Fix => sim?.FindLunchFix();

        /// <summary>True while the guide waits for the world, not the player: the sim runs fast.</summary>
        public bool FastForward(Simulation s)
        {
            if (!Active) return false;
            if (Step == 6) return s.Phase == Phases.Live && s.S.Minute < s.Gd.Slots[0];
            if (Step == 8) return s.S.Till < 1 && s.Phase == Phases.Live;
            if (Step == 11) return s.Phase == Phases.Live;
            return false;
        }

        /// <summary>True while the guide waits for the player: the sim is paused.</summary>
        public bool WantsPause(Simulation s) => Active && !FastForward(s);

        public void Begin(Simulation s, GameActions actions, int resumeStep = 0)
        {
            sim = s;
            Active = true;
            Step = Math.Max(1, Math.Min(Steps, resumeStep));
            if (Step == 1) Analytics("onboarding_begin", new Dictionary<string, object>());
            Sync(s, actions);
            if (Active) Persist(Step, false);
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

        /// <summary>GameActions.Allowed: during the guide only the step's action (and harmless UI) may run.</summary>
        public bool Allows(string action)
        {
            if (!Active) return true;
            foreach (var h in Harmless) if (action.StartsWith(h, StringComparison.Ordinal)) return true;
            return Matches(action);
        }

        public bool Matches(string action)
        {
            string need = Step >= 1 && Step <= Steps ? Fill(Need[Step]) : "";
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
        public void OnAction(Simulation s, GameActions actions, string id, ActionResult r)
        {
            if (!Active) return;
            sim = s;
            if (r.Error == "blocked") { WrongTap(); return; }
            if (!r.Ok || !Matches(id)) return;
            Advance(s, actions);
        }

        void Advance(Simulation s, GameActions actions)
        {
            Analytics("onboarding_step", new Dictionary<string, object> { { "step", (long)Step } });
            Step++;
            if (Step > Steps) { Finish(s, true); return; }
            Sync(s, actions);
            if (Active) Persist(Step, false);
        }

        /// <summary>Every frame: runs the world while the guide waits for it, advances step 6, keeps the week alive.</summary>
        public void Drive(Simulation s, GameActions actions, double realDt)
        {
            if (!Active) return;
            sim = s;
            if (s.Phase == Phases.Plan && Step >= 6) s.StartWeek();
            if (FastForward(s))
            {
                s.SetPaused(false);
                // the first bell change is watched (~3 s); the rest of the week flies (~100 game minutes per second)
                s.AdvanceMinutes(Math.Max(0, realDt) * (Step == 6 ? 4.0 : 100.0));
            }
            else s.SetPaused(true);
            if (Step == 6 && (s.Phase != Phases.Live || s.S.Minute >= s.Gd.Slots[0])) Advance(s, actions);
            else Sync(s, actions);
        }

        /// <summary>Recovery: re-derives a step whose preconditions are gone (relaunch, the screen was left, the week moved
        /// on). Never sends the player forward past an action they still have to learn, never locks them.</summary>
        public void Sync(Simulation s, GameActions actions)
        {
            if (!Active) return;
            sim = s;
            int hall = s.MainHall(), guide = s.Gd.CourseIndex(s.Gd.GuideCourse);
            int guard = 0;
            while (guard++ < Steps + 2)
            {
                int before = Step;
                bool placed = guide >= 0 && s.S.Plan.Exists(p => p.Course == guide && p.Day == s.Gd.GuideDay && p.Slot == s.Gd.GuideSlot);
                switch (Step)
                {
                    case 1:
                    case 2:
                    case 3:
                        if (placed || s.Phase != Phases.Plan) { Step = 4; break; }
                        if (Step >= 2 && (actions == null || actions.BoardHall != hall)) Step = 1;
                        else if (Step == 3 && !(actions.SelectedDay == s.Gd.GuideDay && actions.SelectedSlot == s.Gd.GuideSlot)) Step = 2;
                        break;
                    case 4:
                        if (guide >= 0 && s.S.CourseProf[guide] >= 0) Step = 5;
                        break;
                    case 5:
                        if (s.Phase != Phases.Plan) Step = 6;
                        break;
                    case 6:
                        if (s.Phase == Phases.Report || (s.Phase == Phases.Live && s.S.Minute >= s.Gd.Slots[0])) Step = 7;
                        break;
                    case 7:
                        if (GuideGate(s) < 0 || s.S.OpenGates.Contains(GuideGate(s)) || !s.CanBuy("gate:" + GuideGate(s)).Ok) Step = 8;
                        break;
                    case 8:
                        // nothing will reach the till any more (banked at a week end meanwhile): let the tap pass
                        if (s.S.Till < 1 && s.Phase != Phases.Live) s.S.Till = 1;
                        break;
                    case 9:
                    {
                        string lib = LibrarySite(s);
                        if (lib == "" || !s.CanBuy("build:" + lib).Ok) Step = 10;
                        break;
                    }
                    case 10:
                        if (s.Phase != Phases.Live || s.FindLunchFix() == null) Step = 11;
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
                case "lunch": return e.B > 0 ? "tip.lunch" : "";
                case "bell": return e.B > 0 && s.S.Week >= 2 ? "tip.late" : "";
                case "zone": return e.Text == "quad" ? "tip.quad" : e.Text == "science" ? "tip.science" : "";
                case "built": return e.Text == "dorm" ? "tip.dorm" : e.Text == "lab" ? "tip.lab" : "";
                case "week_start": return e.B == 1 ? "tip.exam" : "";
                case "research_done":
                    return e.Text == "staggered_bells" ? "tip.staggered" : e.Text == "shuttle" ? "tip.shuttle" : e.Text == "wide_paths" ? "tip.widen" : "";
                case "decision": return "tip.decision";
                case "crisis":
                    switch (e.Text)
                    {
                        case "sick_prof": return "tip.sick";
                        case "burst_pipe": return "tip.pipe";
                        case "snow_day": return "tip.snow";
                        case "open_day": return "tip.open_day";
                    }
                    return "";
                case "week_done": return e.A == 1 ? "tip.report" : "";
            }
            return "";
        }
    }
}
