// Charge Nurse — every player action goes through here (UI buttons, world taps, the guide's checks, tests). Engine-free.
// Action ids (the guide's targets use the same names):
//   select_patient:<id>   tap a waiting patient (opens the card: symptoms + colour buttons)
//   select_bed:<b>        tap a bay / ward bed (with a tagged patient selected and a free bay = send them there)
//   tag:<id>:<0-3>        put a colour on the card (green / yellow / orange / red)
//   assign:<b>            send the selected patient to bay b
//   discharge:<id>        sign the discharge now      clean:<b>   clean a dirty bed now
//   ambulance:accept | ambulance:divert                lens       the bottleneck lens (the red corridor)
//   handover              hand the shift over (18:30-19:00)
//   buy:<id>              sim purchase (hire:<team>, tech:<room>, bay, bed:<ward>, room:<kind>, wing:<id>, surge)
//   research:<id>  queue:<rule>  divert:<pct>  start_day  next_day  speed:<n>  pause  resume  open  clear
using System;
using System.Collections.Generic;
using ChargeNurse.Sim;

namespace ChargeNurse.Game
{
    public sealed class ActionResult
    {
        public bool Ok;
        public string Error = "";
        public double Value;
        public string Text = "";
        public static ActionResult Done(double v = 0, string text = "") => new ActionResult { Ok = true, Value = v, Text = text ?? "" };
        public static ActionResult Fail(string e) => new ActionResult { Error = e ?? "failed" };
    }

    public sealed class GameActions
    {
        public readonly GameSession Session;
        public Simulation Sim => Session.Sim;
        public int SelectedPatient { get; private set; } = -1;
        public int SelectedBed { get; private set; } = -1;
        /// <summary>The guide / UI may forbid actions (spotlight on another target). Null = everything allowed.</summary>
        public Func<string, bool> Allowed;
        /// <summary>(action id, result) after every action — the guide advances on these.</summary>
        public event Action<string, ActionResult> Ran;
        public Action<string, Dictionary<string, object>> Analytics = (n, p) => { };
        /// <summary>The last lens result (the HUD's red line + one sentence).</summary>
        public Bottleneck LastLens { get; private set; }

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

        ActionResult Do(string id)
        {
            var a = id.Split(':');
            var sim = Sim;
            switch (a[0])
            {
                case "select_patient":
                {
                    var p = sim.P(Int(a, 1));
                    if (p == null) return ActionResult.Fail("invalid");
                    SelectedPatient = p.Id;
                    SelectedBed = -1;
                    return ActionResult.Done(p.Id);
                }
                case "select_bed":
                {
                    int b = Int(a, 1);
                    if (b < 0 || b >= sim.S.Beds.Count) return ActionResult.Fail("invalid");
                    SelectedBed = b;
                    return ActionResult.Done(b);
                }
                case "clear":
                    SelectedPatient = -1; SelectedBed = -1;
                    return ActionResult.Done();
                case "tag":
                {
                    string e = sim.TagPatient(Int(a, 1), Int(a, 2));
                    return e == "" ? ActionResult.Done(Int(a, 2)) : ActionResult.Fail(e);
                }
                case "assign":
                {
                    if (SelectedPatient < 0) return ActionResult.Fail("no_patient");
                    string e = sim.AssignBay(SelectedPatient, Int(a, 1));
                    if (e != "") return ActionResult.Fail(e);
                    SelectedPatient = -1;
                    return ActionResult.Done();
                }
                case "discharge":
                {
                    string e = sim.Discharge(Int(a, 1));
                    if (e != "") return ActionResult.Fail(e);
                    if (SelectedPatient == Int(a, 1)) SelectedPatient = -1;
                    return ActionResult.Done();
                }
                case "clean":
                {
                    string e = sim.CleanBed(Int(a, 1));
                    return e == "" ? ActionResult.Done() : ActionResult.Fail(e);
                }
                case "ambulance":
                {
                    if (sim.S.Ambulance == null) return ActionResult.Fail("no_call");
                    bool accept = a.Length > 1 && a[1] == "accept";
                    sim.DecideAmbulance(accept);
                    Analytics("ambulance_answer", new Dictionary<string, object> { { "accept", accept ? 1L : 0L } });
                    return ActionResult.Done(accept ? 1 : 0);
                }
                case "lens":
                {
                    LastLens = sim.Lens();
                    Analytics("lens_used", new Dictionary<string, object> { { "key", LastLens.Key } });
                    return ActionResult.Done(LastLens.Count, LastLens.Key);
                }
                case "handover":
                {
                    string e = sim.Handover();
                    if (e != "") return ActionResult.Fail(e);
                    SelectedPatient = -1; SelectedBed = -1;
                    Session.Save();
                    return ActionResult.Done();
                }
                case "open":
                    return ActionResult.Done();
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
                    Analytics("research_start", new Dictionary<string, object> { { "id", a[1] } });
                    Session.Save();
                    return ActionResult.Done();
                }
                case "queue":
                {
                    string e = sim.SetQueue(a.Length > 1 ? a[1] : "");
                    return e == "" ? ActionResult.Done() : ActionResult.Fail(e);
                }
                case "divert":
                {
                    string e = sim.SetDivert(Int(a, 1, 0));
                    return e == "" ? ActionResult.Done() : ActionResult.Fail(e);
                }
                case "start_day":
                    if (!sim.StartDay()) return ActionResult.Fail("phase");
                    sim.SetSpeed(Math.Max(1, Session.Settings.SpeedDefault));
                    Session.Save();
                    return ActionResult.Done();
                case "next_day":
                    if (!sim.NextDay()) return ActionResult.Fail("phase");
                    SelectedPatient = -1; SelectedBed = -1;
                    Session.Save();
                    return ActionResult.Done();
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
