// Bottle Route — every player action goes through here (UI buttons, world taps, the guide's checks, tests). Engine-free.
// Action ids (the guide's targets use the same names):
//   select_point:<i>   tap a return point (with a truck selected = add / remove it on the route draft)
//   select_truck:<i>   tap a truck (starts a route draft for it)
//   go                 dispatch the selected truck along the draft (or add the draft stops to a truck on the road)
//   suggest            fill the draft with the planner's route
//   clear              clear the draft / deselect
//   recall:<t>         call a truck home
//   depot | baler | till | sell:<m> | fuel_info
//   policy:<i>:<threshold|fixed|call>:<value>
//   buy:<id>           sim purchase (machine:<node>, bin_m:<i>, driver, …)
//   research:<id>
//   tech:<point|baler|truck>:<i>
//   auto:<t>:<0|1>
//   start_day | next_day | speed:<n> | pause | resume
using System;
using System.Collections.Generic;
using BottleRoute.Sim;

namespace BottleRoute.Game
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
        public int SelectedTruck { get; private set; } = -1;
        public int SelectedPoint { get; private set; } = -1;
        public readonly List<int> Draft = new List<int>();
        /// <summary>The guide / UI may forbid actions (spotlight on another target). Null = everything allowed.</summary>
        public Func<string, bool> Allowed;
        /// <summary>(action id, result) after every action — the guide advances on these.</summary>
        public event Action<string, ActionResult> Ran;
        public Action<string, Dictionary<string, object>> Analytics = (n, p) => { };

        public GameActions(GameSession session) { Session = session; }

        public RouteEstimate DraftEstimate => SelectedTruck >= 0 && Draft.Count > 0 ? Sim.Estimate(SelectedTruck, Draft) : null;

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

        ActionResult Do(string id)
        {
            var a = id.Split(':');
            var sim = Sim;
            switch (a[0])
            {
                case "select_point":
                {
                    int i = Int(a, 1);
                    if (i < 0 || i >= sim.S.Points.Count) return ActionResult.Fail("invalid");
                    SelectedPoint = i;
                    if (SelectedTruck >= 0)
                    {
                        if (Draft.Contains(i)) Draft.Remove(i);
                        else
                        {
                            if (!sim.CanServe(sim.S.Trucks[SelectedTruck], sim.S.Points[i])) return ActionResult.Fail("unreachable");
                            Draft.Add(i);
                        }
                    }
                    return ActionResult.Done(i);
                }
                case "select_truck":
                {
                    int t = Int(a, 1);
                    if (t < 0 || t >= sim.S.Trucks.Count) return ActionResult.Fail("invalid");
                    if (SelectedTruck != t) Draft.Clear();
                    SelectedTruck = t;
                    SelectedPoint = -1;
                    return ActionResult.Done(t);
                }
                case "clear":
                    Draft.Clear(); SelectedTruck = -1; SelectedPoint = -1;
                    return ActionResult.Done();
                case "suggest":
                {
                    if (SelectedTruck < 0) return ActionResult.Fail("no_truck");
                    var r = sim.SuggestRoute(SelectedTruck, true);
                    Draft.Clear();
                    Draft.AddRange(r);
                    return r.Count > 0 ? ActionResult.Done(r.Count) : ActionResult.Fail("nothing_due");
                }
                case "go":
                {
                    if (SelectedTruck < 0) return ActionResult.Fail("no_truck");
                    if (Draft.Count == 0) return ActionResult.Fail("no_stops");
                    var t = sim.S.Trucks[SelectedTruck];
                    string err;
                    if (t.Busy)
                    {
                        err = "";
                        foreach (int i in Draft) { string e = sim.AddStop(SelectedTruck, i); if (e != "") err = e; }
                    }
                    else err = sim.Dispatch(SelectedTruck, new List<int>(Draft));
                    if (err != "") return ActionResult.Fail(err);
                    Analytics("route_dispatch", new Dictionary<string, object> { { "stops", (long)Draft.Count }, { "truck", t.Type } });
                    Draft.Clear();
                    SelectedTruck = -1;
                    Session.Save();
                    return ActionResult.Done();
                }
                case "recall":
                {
                    string e = sim.Recall(Int(a, 1, SelectedTruck));
                    return e == "" ? ActionResult.Done() : ActionResult.Fail(e);
                }
                case "depot":
                case "baler":
                case "fuel_info":
                case "open":
                    return ActionResult.Done();
                case "till":
                    return ActionResult.Done(sim.CollectTill());
                case "sell":
                {
                    double got = sim.Sell(Int(a, 1, 0));
                    if (got <= 0) return ActionResult.Fail("nothing");
                    Session.Save();
                    return ActionResult.Done(got);
                }
                case "policy":
                {
                    string e = sim.SetPolicy(Int(a, 1), a.Length > 2 ? a[2] : "", Int(a, 3, 0));
                    if (e != "") return ActionResult.Fail(e);
                    Session.Save();
                    return ActionResult.Done();
                }
                case "buy":
                {
                    string item = id.Substring(4);
                    string e = sim.Buy(item);
                    if (e != "") return ActionResult.Fail(e);
                    Analytics("sim_purchase", new Dictionary<string, object> { { "item", item.Split(':')[0] } });
                    Session.Save();
                    return ActionResult.Done();
                }
                case "research":
                {
                    string e = sim.StartResearch(a.Length > 1 ? a[1] : "");
                    if (e != "") return ActionResult.Fail(e);
                    Session.Save();
                    return ActionResult.Done();
                }
                case "tech":
                {
                    string e = sim.SendTech(a.Length > 1 ? a[1] : "", Int(a, 2, 0));
                    return e == "" ? ActionResult.Done() : ActionResult.Fail(e);
                }
                case "auto":
                {
                    string e = sim.SetAuto(Int(a, 1), Int(a, 2, 0) == 1);
                    return e == "" ? ActionResult.Done() : ActionResult.Fail(e);
                }
                case "start_day":
                    if (!sim.StartDay()) return ActionResult.Fail("phase");
                    sim.SetSpeed(Math.Max(1, Session.Settings.SpeedDefault));
                    Session.Save();
                    return ActionResult.Done();
                case "next_day":
                    if (!sim.NextDay()) return ActionResult.Fail("phase");
                    Draft.Clear(); SelectedTruck = -1;
                    Session.Save();
                    return ActionResult.Done();
                case "speed":
                {
                    int s = Int(a, 1, 1);
                    sim.SetSpeed(s);
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
