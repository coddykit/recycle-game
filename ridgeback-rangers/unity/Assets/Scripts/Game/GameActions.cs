// Ridgeback Rangers — every player action goes through here (UI buttons, world taps, the guide's checks, tests). Engine-free.
// Action ids (the guide's targets use the same names):
//   badge | camera | open | clear | pause | resume       harmless UI (the Chief's badge, the camera moved, a sheet opened)
//   pin:<id>                select a pin (the pin card lists the rangers who can take it)
//   send:<pin>:<ranger>     send that ranger (index) to the pin
//   fed_ok                  the "Fed!" toast was tapped (guide)
//   gate | tram_go | tram_policy:<full|interval>:<min>
//   animal:<i>              tap an animal: the first tap picks it, the second opens the Compatibility card for the pair
//   move:<animal>:<paddock> rehome an animal (-1 = nursery)
//   feed:<zone>:<crates>    the feed board's morning delivery for a zone
//   buy:<id>                sim purchase (paddock:<node>, fence:<p>, hire:<role>, tram_car, …)
//   dig:<site>              send a dig crew   ·   temp:<slot>:<0..100>   the incubator's temperature check
//   chief:<0|1>             the Chief Ranger automation on / off
//   start_day | next_day | speed:<n>
using System;
using System.Collections.Generic;
using RidgebackRangers.Sim;

namespace RidgebackRangers.Game
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
        public int SelectedPin { get; private set; } = -1;
        /// <summary>The Compatibility card's pair (animal indices); -1 = not picked.</summary>
        public int CompareFirst { get; private set; } = -1;
        public int CompareSecond { get; private set; } = -1;
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

        /// <summary>Clears the Compatibility pair (the card was closed).</summary>
        public void ClearCompare() { CompareFirst = -1; CompareSecond = -1; }

        ActionResult Do(string id)
        {
            var a = id.Split(':');
            var sim = Sim;
            switch (a[0])
            {
                case "badge":
                case "camera":
                case "open":
                case "fed_ok":
                    return ActionResult.Done();
                case "clear":
                    SelectedPin = -1;
                    ClearCompare();
                    return ActionResult.Done();
                case "pin":
                {
                    var p = sim.Pin(Int(a, 1));
                    if (p == null) return ActionResult.Fail("gone");
                    SelectedPin = p.Id;
                    return ActionResult.Done(p.Id);
                }
                case "send":
                {
                    int pin = Int(a, 1), ranger = Int(a, 2);
                    var p = sim.Pin(pin);
                    string kind = p?.Kind ?? "";
                    string e = sim.Send(pin, ranger);
                    if (e != "") return ActionResult.Fail(e);
                    Analytics("pin_dispatch", new Dictionary<string, object> { { "kind", kind }, { "role", sim.S.Rangers[ranger].Role } });
                    SelectedPin = -1;
                    return ActionResult.Done();
                }
                case "gate": return Of(sim.OpenGate());
                case "tram_go": return Of(sim.TramGo());
                case "tram_policy":
                {
                    string e = sim.SetTramPolicy(a.Length > 1 ? a[1] : "", Int(a, 2, 20));
                    if (e == "") Session.Save();
                    return Of(e);
                }
                case "animal":
                {
                    int i = Int(a, 1);
                    if (i < 0 || i >= sim.S.Animals.Count) return ActionResult.Fail("invalid");
                    if (CompareFirst < 0 || CompareSecond >= 0 || CompareFirst == i) { CompareFirst = i; CompareSecond = -1; return ActionResult.Done(-1); }
                    CompareSecond = i;
                    var x = sim.S.Animals[CompareFirst];
                    var y = sim.S.Animals[CompareSecond];
                    return ActionResult.Done(sim.Harmony(x, y));
                }
                case "move":
                {
                    string e = sim.MoveAnimal(Int(a, 1), Int(a, 2, -2));
                    if (e == "") Session.Save();
                    return Of(e);
                }
                case "feed":
                {
                    string e = sim.SetFeedPlan(a.Length > 1 ? a[1] : "", Int(a, 2, 0));
                    if (e == "") Session.Save();
                    return Of(e);
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
                case "dig":
                {
                    string e = sim.Dig(Int(a, 1));
                    if (e == "") { Analytics("dig_start", new Dictionary<string, object> { { "site", (long)Int(a, 1) } }); Session.Save(); }
                    return Of(e);
                }
                case "temp":
                {
                    string e = sim.CheckTemp(Int(a, 1), Int(a, 2, -1));
                    if (e == "") Session.Save();
                    return Of(e);
                }
                case "chief": return Of(sim.SetChief(Int(a, 1, 0) == 1));
                case "start_day":
                    if (!sim.StartDay()) return ActionResult.Fail("phase");
                    sim.SetSpeed(Math.Max(1, Session.Settings.SpeedDefault));
                    Session.Save();
                    return ActionResult.Done();
                case "next_day":
                    if (!sim.NextDay()) return ActionResult.Fail("phase");
                    SelectedPin = -1;
                    ClearCompare();
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
