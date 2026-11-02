// Pour & Roast — every player action goes through here (UI buttons, world taps, the guide's checks, tests). Engine-free.
// Action ids (the guide's targets use the same names):
//   select_ticket:<id>   tap a ticket on the rail (the ticket bar opens: Brew next · Priority · Cancel)
//   brew                 tap the espresso machine with a ticket selected: it starts now / leads the rail
//   flag:<id>            priority flag on / off            cancel:<id>   remove a ticket not started (duplicates)
//   handover             tap the pickup shelf: every finished cup goes out now (+ a small tip)
//   fridge | forecast | open | station:<id>   info taps (open the right sheet; the guide counts some)
//   dial_in              hold the grinder: the grind is tuned again       repair   fix a jammed grinder
//   rule:<fifo|deadline|vip>   batch:<on|off>   assign:<staff>:<espresso|milk|any>
//   menu:<item>:<0|1>   par:<pastry>:<n>   milk:<0|1>:<litres>   supplier:<id>   auto_beans:<0|1>   prep:<hot|ice>
//   buy:<id>             sim purchase (staff:barista, heads, venue:corner, milk_run, green:<origin>, beans, …)
//   research:<id>   venue:<id> (switch branch)   roast:<origin>:<profile>   drop   blend   contract:<id>
//   start_day | next_day | speed:<n> | pause | resume
using System;
using System.Collections.Generic;
using PourAndRoast.Sim;

namespace PourAndRoast.Game
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
        /// <summary>The selected ticket id (UI state, never saved), -1 = none.</summary>
        public int SelectedTicket { get; private set; } = -1;
        /// <summary>The guide / UI may forbid actions (spotlight on another target). Null = everything allowed.</summary>
        public Func<string, bool> Allowed;
        /// <summary>(action id, result) after every action — the guide advances on these.</summary>
        public event Action<string, ActionResult> Ran;
        public Action<string, Dictionary<string, object>> Analytics = (n, p) => { };

        public GameActions(GameSession session) { Session = session; }

        public TicketState Selected => SelectedTicket >= 0 ? Sim.Ticket(SelectedTicket) : null;

        /// <summary>UI / guide: select a ticket without running an action (the guide pre-selects the pre-order).</summary>
        public void Select(int ticketId) => SelectedTicket = ticketId;

        public ActionResult Run(string id)
        {
            if (string.IsNullOrEmpty(id)) return ActionResult.Fail("invalid");
            if (Allowed != null && !Allowed(id)) return Report(id, ActionResult.Fail("blocked"));
            ActionResult r;
            try { r = Do(id); }
            catch (Exception e) { r = ActionResult.Fail("exception_" + e.GetType().Name); }
            if (SelectedTicket >= 0 && Sim.Ticket(SelectedTicket) == null) SelectedTicket = -1;
            return Report(id, r);
        }

        ActionResult Report(string id, ActionResult r)
        {
            Ran?.Invoke(id, r);
            return r;
        }

        static int Int(string[] a, int i, int def = -1) => a.Length > i && int.TryParse(a[i], out int v) ? v : def;
        static string Arg(string[] a, int i) => a.Length > i ? a[i] : "";
        static ActionResult Of(string err) => err == "" ? ActionResult.Done() : ActionResult.Fail(err);

        ActionResult Saved(string err)
        {
            if (err != "") return ActionResult.Fail(err);
            Session.Save();
            return ActionResult.Done();
        }

        ActionResult Do(string id)
        {
            var a = id.Split(':');
            var sim = Sim;
            switch (a[0])
            {
                case "select_ticket":
                {
                    var t = sim.Ticket(Int(a, 1));
                    if (t == null) return ActionResult.Fail("invalid");
                    SelectedTicket = SelectedTicket == t.Id ? -1 : t.Id;
                    return ActionResult.Done(t.Id);
                }
                case "deselect": SelectedTicket = -1; return ActionResult.Done();
                case "brew":
                {
                    int t = a.Length > 1 ? Int(a, 1) : SelectedTicket;
                    if (t < 0) return ActionResult.Fail("no_ticket");
                    string e = sim.Brew(t);
                    if (e == "") SelectedTicket = -1;
                    return Of(e);
                }
                case "flag": return Of(sim.Flag(a.Length > 1 ? Int(a, 1) : SelectedTicket));
                case "cancel":
                {
                    string e = sim.Cancel(a.Length > 1 ? Int(a, 1) : SelectedTicket);
                    if (e == "") SelectedTicket = -1;
                    return Of(e);
                }
                case "handover":
                {
                    int n = sim.HandoverAll();
                    return n > 0 ? ActionResult.Done(n) : ActionResult.Fail("nothing_ready");
                }
                case "fridge":
                case "forecast":
                case "open":
                case "station":
                case "reviews":
                    return ActionResult.Done();
                case "dial_in": return Of(sim.DialIn());
                case "repair": return Of(sim.Repair());
                case "rule": return Saved(sim.SetRule(Arg(a, 1)));
                case "batch": return Saved(sim.SetBatch(Arg(a, 1) == "on"));
                case "assign": return Saved(sim.Assign(Int(a, 1), Arg(a, 2)));
                case "menu": return Saved(sim.ToggleMenu(Arg(a, 1), Int(a, 2, 0) == 1));
                case "par": return Saved(sim.SetPar(Arg(a, 1), Int(a, 2, 0)));
                case "milk": return Saved(sim.SetMilkOrder(Int(a, 1), Int(a, 2, 0)));
                case "supplier": return Saved(sim.SetSupplier(Arg(a, 1)));
                case "auto_beans": sim.S.AutoBeans = Int(a, 1, 0) == 1; Session.Save(); return ActionResult.Done();
                case "prep": return Saved(sim.Prep(Arg(a, 1)));
                case "buy":
                {
                    string item = id.Substring(4);
                    string e = sim.Buy(item);
                    if (e != "") return ActionResult.Fail(e);
                    Analytics("sim_purchase", new Dictionary<string, object> { { "item", item.Split(':')[0] } });
                    if (item.StartsWith("venue:", StringComparison.Ordinal)) Analytics("venue_unlock", new Dictionary<string, object> { { "venue", item.Substring(6) } });
                    Session.Save();
                    return ActionResult.Done();
                }
                case "research":
                {
                    string e = sim.StartResearch(Arg(a, 1));
                    if (e != "") return ActionResult.Fail(e);
                    Analytics("research_start", new Dictionary<string, object> { { "id", Arg(a, 1) } });
                    Session.Save();
                    return ActionResult.Done();
                }
                case "venue": return Saved(sim.SwitchVenue(Arg(a, 1)));
                case "roast":
                {
                    string e = sim.StartRoast(Arg(a, 1), Arg(a, 2));
                    if (e != "") return ActionResult.Fail(e);
                    Analytics("roast_start", new Dictionary<string, object> { { "origin", Arg(a, 1) }, { "profile", Arg(a, 2) } });
                    Session.Save();
                    return ActionResult.Done();
                }
                case "drop":
                {
                    bool sweet = sim.InSweetSpot;
                    string e = sim.Drop();
                    if (e != "") return ActionResult.Fail(e);
                    Session.Save();
                    return ActionResult.Done(sweet ? 1 : 0);
                }
                case "blend":
                {
                    double kg = sim.Blend();
                    if (kg <= 0) return ActionResult.Fail("nothing_rested");
                    Session.Save();
                    return ActionResult.Done(kg);
                }
                case "contract": return Saved(sim.ToggleContract(Arg(a, 1)));
                case "start_day":
                    if (!sim.StartDay()) return ActionResult.Fail("phase");
                    sim.SetSpeed(Math.Max(1, Session.Settings.SpeedDefault));
                    Session.Save();
                    return ActionResult.Done();
                case "next_day":
                    if (!sim.NextDay()) return ActionResult.Fail("phase");
                    SelectedTicket = -1;
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
