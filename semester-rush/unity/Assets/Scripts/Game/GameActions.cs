// Semester Rush — every player action goes through here (UI buttons, world taps, the guide's checks, tests). Engine-free.
// Action ids (the guide's targets use the same names):
//   hall:<node>                 tap a lecture hall / lab (opens its timetable board)
//   building:<node> | site:<node> | open | coins_info     harmless UI (info cards)
//   cell:<day>:<slot>           tap a board cell of the open hall: empty + a class picked -> move it there; a class in
//                               the cell -> pick it; empty -> choose a course for it
//   course:<c>                  put course c in the chosen cell
//   prof:<c>:<p>                assign professor p to course c
//   move:<i>:<day>:<slot>:<node>   move class i      remove:<i>      auto_fill      clear_sel
//   gate:<edge>                 open a gate (a shortcut)
//   buy:<id>                    sim purchase (build:<site>, widen:<edge>, professor:<faculty>, staff:<dept>, zone:<id>)
//   dismiss:<dept> | research:<id> | till | staggered:<0|1> | decide:<option> | intake:<choice>
//   start_week | report | next_week | speed:<n> | pause | resume
using System;
using System.Collections.Generic;
using SemesterRush.Sim;

namespace SemesterRush.Game
{
    public sealed class ActionResult
    {
        public bool Ok;
        public string Error = "";
        public double Value;
        public static ActionResult Done(double v = 0) => new ActionResult { Ok = true, Value = v };
        public static ActionResult Fail(string e) => new ActionResult { Error = e ?? "failed" };
    }

    public sealed class GameActions
    {
        public readonly GameSession Session;
        public Simulation Sim => Session.Sim;
        /// <summary>The room whose board is open (-1 none).</summary>
        public int BoardHall { get; private set; } = -1;
        public int SelectedDay { get; private set; } = -1;
        public int SelectedSlot { get; private set; } = -1;
        public int SelectedPlacement { get; private set; } = -1;
        public bool CellChosen => SelectedDay >= 0 && SelectedSlot >= 0;
        /// <summary>The guide / UI may forbid actions (spotlight on another target). Null = everything allowed.</summary>
        public Func<string, bool> Allowed;
        /// <summary>(action id, result) after every action — the guide advances on these.</summary>
        public event Action<string, ActionResult> Ran;
        public Action<string, Dictionary<string, object>> Analytics = (n, p) => { };

        public GameActions(GameSession session) { Session = session; }

        public ActionResult Run(string id)
        {
            if (string.IsNullOrEmpty(id)) return ActionResult.Fail("invalid");
            if (Allowed != null && !Allowed(id)) return Report(id, ActionResult.Fail("blocked"));
            ActionResult r;
            try { r = Do(id); }
            catch (Exception e) { r = ActionResult.Fail("exception_" + e.GetType().Name); }
            return Report(id, r);
        }

        ActionResult Report(string id, ActionResult r)
        {
            Ran?.Invoke(id, r);
            return r;
        }

        static int Int(string[] a, int i, int def = -1) => a.Length > i && int.TryParse(a[i], out int v) ? v : def;

        public void ClearSelection() { SelectedDay = SelectedSlot = SelectedPlacement = -1; }

        /// <summary>The UI picks a class directly (the guide's lunch step, a session card).</summary>
        public void SelectPlacement(int i) { SelectedPlacement = i; SelectedDay = SelectedSlot = -1; }

        ActionResult Saved(string err, double v = 0)
        {
            if (err != "") return ActionResult.Fail(err);
            Session.Save();
            return ActionResult.Done(v);
        }

        ActionResult Do(string id)
        {
            var a = id.Split(':');
            var sim = Sim;
            switch (a[0])
            {
                case "hall":
                {
                    int node = Int(a, 1);
                    var b = sim.BuildingAt(node);
                    if (b == null || !b.Built || !sim.DefOf(b).Teaching) return ActionResult.Fail("not_room");
                    if (BoardHall != node) ClearSelection();
                    BoardHall = node;
                    return ActionResult.Done(node);
                }
                case "building":
                case "site":
                case "open":
                case "coins_info":
                    return ActionResult.Done();
                case "cell":
                {
                    int day = Int(a, 1), slot = Int(a, 2);
                    if (BoardHall < 0) return ActionResult.Fail("no_board");
                    if (day < 0 || day >= sim.Gd.DaysPerWeek || slot < 0 || slot >= sim.Gd.SlotCount) return ActionResult.Fail("invalid");
                    int here = sim.At(day, slot, BoardHall);
                    if (here >= 0) { SelectPlacement(here); return ActionResult.Done(here); }
                    if (SelectedPlacement >= 0 && SelectedPlacement < sim.ActivePlan.Count)
                    {
                        string err = sim.Move(SelectedPlacement, day, slot, BoardHall);
                        if (err != "") return ActionResult.Fail(err);
                        ClearSelection();
                        Session.Save();
                        return ActionResult.Done();
                    }
                    if (sim.Past(day, slot)) return ActionResult.Fail("past");
                    SelectedDay = day; SelectedSlot = slot; SelectedPlacement = -1;
                    return ActionResult.Done();
                }
                case "course":
                {
                    int c = Int(a, 1);
                    if (BoardHall < 0 || !CellChosen) return ActionResult.Fail("no_cell");
                    string err = sim.Place(c, SelectedDay, SelectedSlot, BoardHall);
                    if (err != "") return ActionResult.Fail(err);
                    ClearSelection();
                    return Saved("");
                }
                case "prof": return Saved(sim.AssignProf(Int(a, 1), Int(a, 2)));
                case "move":
                {
                    string err = sim.Move(Int(a, 1), Int(a, 2), Int(a, 3), Int(a, 4));
                    if (err == "") ClearSelection();
                    return Saved(err);
                }
                case "remove":
                {
                    string err = sim.Remove(Int(a, 1));
                    if (err == "") ClearSelection();
                    return Saved(err);
                }
                case "auto_fill":
                {
                    int n = sim.AutoFill(sim.SmartFill);
                    Analytics("timetable_auto_fill", new Dictionary<string, object> { { "placed", (long)n }, { "smart", sim.SmartFill ? 1L : 0L } });
                    if (n <= 0) return ActionResult.Fail(sim.Missing > 0 ? "no_room_left" : "nothing_missing");
                    return Saved("", n);
                }
                case "clear_sel": ClearSelection(); return ActionResult.Done();
                case "gate":
                {
                    string e = sim.Buy("gate:" + Int(a, 1));
                    if (e == "") Analytics("sim_purchase", new Dictionary<string, object> { { "item", "gate" } });
                    return Saved(e);
                }
                case "buy":
                {
                    string item = id.Substring(4);
                    string e = sim.Buy(item);
                    if (e == "") Analytics("sim_purchase", new Dictionary<string, object> { { "item", item.Split(':')[0] } });
                    return Saved(e);
                }
                case "dismiss": return Saved(sim.Dismiss(a.Length > 1 ? a[1] : ""));
                case "research":
                {
                    string e = sim.StartResearch(a.Length > 1 ? a[1] : "");
                    if (e == "") Analytics("research_start", new Dictionary<string, object> { { "id", a[1] } });
                    return Saved(e);
                }
                case "till": return ActionResult.Done(sim.CollectTill());
                case "staggered": return Saved(sim.SetStaggered(Int(a, 1, 0) == 1));
                case "decide":
                {
                    if (sim.S.Pending == null) return ActionResult.Fail("invalid");
                    return Saved(sim.Decide(sim.S.Pending.Id, a.Length > 1 ? a[1] : ""));
                }
                case "intake": return Saved(sim.SetIntake(a.Length > 1 ? a[1] : ""));
                case "start_week":
                    if (!sim.StartWeek()) return ActionResult.Fail("phase");
                    sim.SetSpeed(Math.Max(1, Session.Settings.SpeedDefault));
                    ClearSelection();
                    return Saved("");
                case "report":
                    return sim.Phase == Phases.Report ? ActionResult.Done() : ActionResult.Fail("phase");
                case "next_week":
                    if (!sim.NextWeek()) return ActionResult.Fail("phase");
                    ClearSelection();
                    return Saved("");
                case "speed":
                {
                    sim.SetSpeed(Int(a, 1, 1));
                    Session.Settings.SpeedDefault = sim.S.Speed;
                    return ActionResult.Done(sim.S.Speed);
                }
                case "pause": sim.SetPaused(true); return ActionResult.Done();
                case "resume": sim.SetPaused(false); return ActionResult.Done();
            }
            return ActionResult.Fail("unknown");
        }
    }
}
