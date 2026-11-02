// Docket Day — every player action goes through here (UI buttons, drags on the Docket Rail, world taps, the guide's checks,
// tests). Engine-free. Action ids (the guide's targets use the same names):
//   reception | courthouse | rail | open | pause | resume        UI only (open a view)
//   matter:<id>                    open a matter card
//   select_staff:<id>              pick a person card (tap-to-assign: the next task tap assigns them; again = deselect)
//   accept:<id> | wall:<id> | decline:<id> | refer:<id>          intake decisions (wall = accept behind an ethical wall)
//   assign:<staff>:<matter>:<task> a person onto a task on the rail (drag, or tap person then task)
//   unassign:<matter>:<task>
//   prep_match:<matter>:<claim>:<exhibit>                        the prep table
//   settle:<m> | visiting:<m> | continuance:<m>                  levers
//   resolve:<m>:<objection>        the hearing scene ended (0 none, 1 good "objection!", 2 bad)
//   bill                           the billing run (report)
//   build:<room> | hire:<role> | fire:<id> | move_up | rule:<area>:<0|1>
//   start_day | next_day | ff | speed:<n>      ff = start the day and run fast to the next decision
using System;
using System.Collections.Generic;
using DocketDay.Sim;

namespace DocketDay.Game
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
        public static readonly HashSet<string> UiOnly = new HashSet<string> { "reception", "courthouse", "rail", "open", "pause", "resume", "prep_open" };

        public readonly GameSession Session;
        public Simulation Sim => Session.Sim;
        public int SelectedStaff { get; private set; } = -1;
        public int SelectedMatter { get; private set; } = -1;
        /// <summary>"ff" pressed: the screen runs the day fast until the next decision (report, a hearing, a walk-in, a crisis).</summary>
        public bool FastForwarding;
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
        static ActionResult Of(string err, double v = 0) => err == "" ? ActionResult.Done(v) : ActionResult.Fail(err);

        ActionResult Saved(string err, double v = 0)
        {
            if (err == "") Session.Save();
            return Of(err, v);
        }

        ActionResult Do(string id)
        {
            var a = id.Split(':');
            var sim = Sim;
            if (UiOnly.Contains(a[0])) return ActionResult.Done();
            switch (a[0])
            {
                case "matter":
                {
                    var m = sim.MatterById(Int(a, 1));
                    if (m == null) return ActionResult.Fail("invalid");
                    SelectedMatter = m.Id;
                    return ActionResult.Done(m.Id);
                }
                case "select_staff":
                {
                    int s = Int(a, 1);
                    if (sim.StaffById(s) == null) return ActionResult.Fail("invalid");
                    SelectedStaff = SelectedStaff == s ? -1 : s;
                    return ActionResult.Done(SelectedStaff);
                }
                case "accept":
                case "wall":
                {
                    var m = sim.MatterById(Int(a, 1));
                    string area = m != null ? m.Area : "";
                    string e = sim.Accept(Int(a, 1), a[0] == "wall");
                    if (e == "") Analytics("matter_accept", new Dictionary<string, object> { { "area", area }, { "wall", a[0] == "wall" ? 1L : 0L } });
                    return Saved(e);
                }
                case "decline":
                    return Saved(sim.Decline(Int(a, 1)));
                case "refer":
                {
                    string e = sim.Refer(Int(a, 1));
                    if (e == "") Analytics("matter_refer", new Dictionary<string, object>());
                    return Saved(e);
                }
                case "assign":
                {
                    string e = sim.Assign(Int(a, 1), Int(a, 2), Int(a, 3));
                    if (e == "") SelectedStaff = -1;
                    return Of(e);
                }
                case "unassign":
                    return Of(sim.Unassign(Int(a, 1), Int(a, 2)));
                case "prep_match":
                    return Of(sim.PrepMatch(Int(a, 1), Int(a, 2), Int(a, 3)));
                case "settle":
                {
                    var m = sim.MatterById(Int(a, 1));
                    double fee = m != null && m.Status == MatterStatus.Open ? sim.SettleFee(m) : 0;
                    string e = sim.Settle(Int(a, 1));
                    if (e == "") Analytics("matter_settle", new Dictionary<string, object> { { "area", m.Area } });
                    return Saved(e, fee);
                }
                case "visiting":
                    return Saved(sim.Visiting(Int(a, 1)));
                case "continuance":
                    return Saved(sim.Continuance(Int(a, 1)));
                case "resolve":
                {
                    int r = sim.Resolve(Int(a, 1), Math.Max(0, Math.Min(2, Int(a, 2, 0))));
                    return r < 0 ? ActionResult.Fail("invalid") : ActionResult.Done(r);
                }
                case "bill":
                {
                    double v = sim.Bill();
                    if (v < 0) return ActionResult.Fail("phase");
                    Analytics("bill_run", new Dictionary<string, object> { { "day", (long)sim.S.Day } });
                    Session.Save();
                    return ActionResult.Done(v);
                }
                case "build":
                {
                    string room = a.Length > 1 ? a[1] : "";
                    string e = sim.Build(room);
                    if (e == "") Analytics("room_build", new Dictionary<string, object> { { "room", room } });
                    return Saved(e);
                }
                case "hire":
                {
                    string role = a.Length > 1 ? a[1] : "";
                    string e = sim.Hire(role);
                    if (e == "") Analytics("staff_hire", new Dictionary<string, object> { { "role", role } });
                    return Saved(e);
                }
                case "fire":
                    return Saved(sim.Fire(Int(a, 1)));
                case "move_up":
                {
                    string e = sim.MoveUp();
                    if (e == "") Analytics("chapter_unlock", new Dictionary<string, object> { { "chapter", (long)sim.Chapter } });
                    return Saved(e);
                }
                case "rule":
                    return Saved(sim.SetRule(a.Length > 1 ? a[1] : "", Int(a, 2, 0) == 1));
                case "start_day":
                    if (!sim.StartDay()) return ActionResult.Fail("phase");
                    sim.SetSpeed(Math.Max(1, Session.Settings.SpeedDefault));
                    Session.Save();
                    return ActionResult.Done();
                case "ff":
                    if (sim.Phase == Phases.Report) return ActionResult.Fail("phase");
                    if (sim.Phase == Phases.Plan) { sim.StartDay(); sim.SetSpeed(Math.Max(1, Session.Settings.SpeedDefault)); }
                    FastForwarding = true;
                    return ActionResult.Done();
                case "next_day":
                    if (!sim.NextDay()) return ActionResult.Fail("phase");
                    SelectedStaff = -1;
                    FastForwarding = false;
                    Session.Save();
                    return ActionResult.Done();
                case "speed":
                {
                    sim.SetSpeed(Int(a, 1, 1));
                    Session.Settings.SpeedDefault = sim.S.Speed;
                    return ActionResult.Done(sim.S.Speed);
                }
            }
            return ActionResult.Fail("unknown");
        }

        public void ClearSelection() { SelectedStaff = -1; }
    }
}
