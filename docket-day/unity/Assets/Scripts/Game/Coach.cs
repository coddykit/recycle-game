// Docket Day — the 9-step show-and-do guide (plan §9, fleet §9). One thing per step; the target is spotlit; the step
// advances ONLY when the player really does that action; a wrong tap repeats the hint (no penalty); time is paused while
// the guide waits for the player and runs fast while it waits for the world (the day passing, the filing, the hearing).
// Safe: the sim is in tutorial mode (no crises, no sanctions, no Standing loss, the first hearing cannot be lost).
// Skip + Settings -> "Replay the guide". Every step is recoverable: leaving the screen / relaunching mid-step re-derives
// the step from the career (Sync), never locks the player (Volt City lesson 11-01).
//   1 tap the client at reception            6 the prep table: match two exhibit cards
//   2 Accept the parking-ticket matter       7 watch the hearing, then the billing run
//   3 drag the intern onto the evidence task 8 build the library
//   4 fast-forward one day (the clock)       9 the first collision: drag the associate onto one hearing
//   5 drag the paralegal onto the filing (amber)
using System;
using System.Collections.Generic;
using DocketDay.Sim;

namespace DocketDay.Game
{
    public sealed class Coach
    {
        public const int Steps = 9;
        public const int Done = 10;

        public int Step { get; private set; }
        public bool Active { get; private set; }
        public int WrongTaps { get; private set; }
        /// <summary>Increments on a wrong tap: the UI pulses the hint again.</summary>
        public int Flash { get; private set; }
        /// <summary>Step 4: the clock was tapped, the day is running fast.</summary>
        public bool Waiting { get; private set; }
        int waitDay;
        public Action<string, Dictionary<string, object>> Analytics = (n, p) => { };
        public Action<int, bool> Persist = (step, done) => { };
        public string BubbleKey => Active && Step >= 1 && Step <= Steps ? (Step == 4 && Waiting ? "coach.4w" : Step == 7 && preBill ? "coach.7w" : "coach." + Step) : "";
        bool preBill;

        /// <summary>What the UI spotlights now: reception, accept:&lt;m&gt;, task:&lt;m&gt;:&lt;i&gt;, staff:&lt;id&gt;, ff, prep:&lt;m&gt;, bill,
        /// build:library. "" = nothing (the guide waits for the world).</summary>
        public string Target(Simulation sim)
        {
            if (!Active || sim == null) return "";
            int t = sim.S.TutorialMatter;
            switch (Step)
            {
                case 1: return "reception";
                case 2: return "accept:" + t;
                case 3: return "task:" + t + ":0";
                case 4: return Waiting ? "" : "ff";
                case 5: return "task:" + t + ":1";
                case 6: return "prep:" + t;
                case 7: return sim.Phase == Phases.Report ? "bill" : "";
                case 8: return "build:library";
                case 9:
                    foreach (int id in sim.S.TutorialCollision)
                    {
                        var m = sim.MatterById(id);
                        if (m != null && m.Status == MatterStatus.Open) return "task:" + id + ":" + (m.Tasks.Count - 1);
                    }
                    return "";
            }
            return "";
        }

        /// <summary>The person card the step suggests (glows next to the task): intern / paralegal / associate.</summary>
        public int SuggestedStaff(Simulation sim)
        {
            if (!Active || sim == null) return -1;
            string role = Step == 3 ? "intern" : Step == 5 ? "paralegal" : Step == 9 ? "associate" : "";
            if (role == "") return -1;
            var s = sim.S.Staff.Find(x => x.Role == role);
            if (s == null && Step == 9) s = sim.S.Staff.Find(x => x.Id != 0 && sim.RoleOf(x).Lawyer);
            return s != null ? s.Id : -1;
        }

        /// <summary>True while the guide waits for the world, not the player: the sim runs fast.</summary>
        public bool FastForward(Simulation sim)
        {
            if (!Active) return false;
            if (Step == 4) return Waiting;
            if (Step == 7) return sim.Phase != Phases.Report;
            return false;
        }

        /// <summary>True while the guide waits for the player: the sim is paused.</summary>
        public bool WantsPause(Simulation sim) => Active && !FastForward(sim);

        public void Begin(Simulation sim, GameActions actions, int resumeStep = 0)
        {
            Active = true;
            Waiting = false;
            Step = Math.Max(1, Math.Min(Steps, resumeStep));
            sim.BeginTutorial(Step <= 1);
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
            Waiting = false;
            sim.EndTutorial();   // a skipped guide leaves an ordinary first client
            sim.SetPaused(false);
            if (completed) Analytics("onboarding_complete", new Dictionary<string, object> { { "wrong_taps", (long)WrongTaps } });
            Step = Done;
            Persist(Done, true);
        }

        static string[] Parts(string a) => (a ?? "").Split(':');

        /// <summary>Would this action complete (or be part of) the current step? Wrong targets are blocked, not run.</summary>
        bool Shape(Simulation sim, string action)
        {
            var p = Parts(action);
            string t = sim.S.TutorialMatter.ToString();
            switch (Step)
            {
                case 1: return action == "reception";
                case 2: return action == "accept:" + t;
                case 3: return p[0] == "assign" && p.Length == 4 && p[2] == t && p[3] == "0";
                case 4: return action == "ff" && !Waiting;
                case 5: return p[0] == "assign" && p.Length == 4 && p[2] == t && p[3] == "1";
                case 6: return p[0] == "prep_match" && p.Length == 4 && p[1] == t;
                case 7: return action == "bill";
                case 8: return action == "build:library";
                case 9:
                    if (p[0] != "assign" || p.Length != 4 || !int.TryParse(p[2], out int mid) || !sim.S.TutorialCollision.Contains(mid)) return false;
                    var m = sim.MatterById(mid);
                    return m != null && p[3] == (m.Tasks.Count - 1).ToString();
            }
            return false;
        }

        /// <summary>GameActions.Allowed: during the guide only the step's action (and harmless UI) may run.</summary>
        public Func<string, bool> AllowsFor(Func<Simulation> sim) => action => Allows(sim(), action);

        public bool Allows(Simulation sim, string action)
        {
            if (!Active) return true;
            var p = Parts(action);
            if (GameActions.UiOnly.Contains(p[0]) || p[0] == "matter" || p[0] == "select_staff") return true;
            return Shape(sim, action);
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
            if (r.Error == "blocked" || r.Error == "wrong") { WrongTap(); return; }
            if (!r.Ok || !Shape(sim, id)) return;
            switch (Step)
            {
                case 4:
                    Waiting = true;
                    var tm = sim.MatterById(sim.S.TutorialMatter);
                    waitDay = (tm != null && tm.Status == MatterStatus.Open ? tm.AcceptDay : sim.S.Day) + 1;
                    if (sim.Phase == Phases.Plan) sim.StartDay();
                    return;
                case 6:
                    var m = sim.MatterById(sim.S.TutorialMatter);
                    if (m != null && !m.PuzzleSolved) return;   // one card matched: the next one
                    break;
                case 9:
                    if (CollisionLeft(sim)) return;
                    break;
            }
            Advance(sim, actions);
        }

        static bool CollisionLeft(Simulation sim)
        {
            var c = sim.Collisions();
            foreach (int id in sim.S.TutorialCollision) if (c.Exists(x => x.Matter == id)) return true;
            return false;
        }

        void Advance(Simulation sim, GameActions actions)
        {
            Analytics("onboarding_step", new Dictionary<string, object> { { "step", (long)Step } });
            Step++;
            Waiting = false;
            if (Step > Steps) { Finish(sim, true); return; }
            Sync(sim, actions);
            if (Active) Persist(Step, false);
        }

        /// <summary>Every frame: runs the world while the guide waits for it, keeps the days moving, ends the waits.
        /// holdWorld = the hearing scene (or an ad) is on screen: the world waits for it.</summary>
        public void Drive(Simulation sim, GameActions actions, double realDt, bool holdWorld = false)
        {
            if (!Active) return;
            preBill = Step == 7 && sim.Phase != Phases.Report;
            if (sim.Phase == Phases.Report && Step != 7) { sim.Bill(); sim.NextDay(); }
            if (sim.Phase == Phases.Plan && Step == 7) sim.StartDay();
            if (FastForward(sim) && !holdWorld)
            {
                sim.SetPaused(false);
                sim.AdvanceMinutes(Math.Max(0, realDt) * 120.0);   // ~2 game hours per real second
            }
            else sim.SetPaused(true);
            if (Step == 4 && Waiting && sim.S.Day >= waitDay && sim.Phase == Phases.Plan) Advance(sim, actions);
            else Sync(sim, actions);
        }

        /// <summary>Recovery: re-derives a step whose preconditions are gone (relaunch, the screen was left, the day moved
        /// on). Never sends the player forward past an action they still have to learn, never locks them.</summary>
        public void Sync(Simulation sim, GameActions actions)
        {
            if (!Active) return;
            int guard = 0;
            while (guard++ < Steps + 2 && Active)
            {
                int before = Step;
                var tm = sim.S.TutorialMatter > 0 ? sim.MatterById(sim.S.TutorialMatter) : null;
                bool closed = sim.S.Seen.Contains("tut_matter_closed");
                if (tm == null && !closed && Step <= 7) { sim.S.TutorialMatter = 0; tm = sim.SeedTutorial(); Step = 1; }
                if (closed && Step <= 6) Step = 7;
                switch (Step)
                {
                    case 1:
                    case 2:
                        if (tm != null && tm.Active) Step = 3;
                        break;
                    case 3:
                        if (tm != null && tm.Tasks.Count > 0 && (tm.Tasks[0].Staff != Attend.None || tm.Tasks[0].Finished)) Step = 4;
                        break;
                    case 4:
                        if (tm != null && sim.S.Day > tm.AcceptDay && sim.Phase == Phases.Plan) Step = 5;
                        else if (!Waiting && sim.Phase == Phases.Live) { Waiting = true; waitDay = (tm != null ? tm.AcceptDay : sim.S.Day) + 1; }
                        break;
                    case 5:
                        if (tm != null && tm.Tasks.Count > 1 && (tm.Tasks[1].Staff != Attend.None || tm.Tasks[1].Finished)) Step = 6;
                        break;
                    case 6:
                        if (tm == null || tm.PuzzleSolved || tm.Status == MatterStatus.Hearing) Step = 7;
                        break;
                    case 7:
                        if (closed && (sim.Phase != Phases.Report || sim.S.Billed)) Step = 8;
                        break;
                    case 8:
                    {
                        var b = sim.CanBuild("library");
                        if (sim.RoomBuilt("library") || sim.S.Building == "library" || (!b.Ok && b.Reason != "")) Step = 9;
                        break;
                    }
                    case 9:
                        sim.SpawnTutorialCollision();
                        if (!CollisionLeft(sim)) { Finish(sim, true); return; }
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
                case "walkin":
                {
                    var m = sim.MatterById(e.A);
                    if (m != null && sim.Conflict(m)) return "tip.conflict";
                    return "";
                }
                case "trust_in": return "tip.trust";
                case "late": return "tip.late";
                case "continued": return e.B == 1 ? "tip.auto_continuance" : "";
                case "revealed": return "tip.twist";
                case "chapter": return e.A == 2 ? "tip.chapter2" : e.A == 3 ? "tip.chapter3" : "";
                case "hearing_start": return "tip.judges";
                case "crisis":
                    switch (e.Text)
                    {
                        case "surprise_motion": return "tip.motion";
                        case "power_cut": return "tip.power_cut";
                        case "sick_day": return "tip.sick";
                        case "substitute_judge": return "tip.substitute";
                    }
                    return "";
                case "day_done": return sim.S.Day <= 2 ? "tip.report" : "";
            }
            return "";
        }
    }
}
