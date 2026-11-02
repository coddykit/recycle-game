// Grill Convoy — every player action goes through here (UI buttons, world taps, the guide's checks, tests). Engine-free.
// Action ids (the guide's targets use the same names):
//   select_truck:<t> | select_pitch:<node>       UI selection (opens the truck / pitch sheet; no state change)
//   pitch:<t>:<node>                             today's pitch for a truck (mid-day: relocate)
//   send:<t> | restock:<t> | recall:<t>          drive out / restock run / home and closed
//   load:<t>:<item>:<delta> | repack:<t> | suggest_pack:<t>     the pack list (truck at the commissary)
//   prep:<item> | par:<item>:<n>                 commissary prep batch / par level
//   buy:<id>                                     pitch:<district>, truck:<type>, station:<t>:<type>, hire:<t>:<role>, extend:<t>
//   role:<t>:<i>:<role> | release:<t>:<i>        crew
//   card:<t>:<card> | tier:<t>:<0..2>            menu board / fixings tier
//   move:<t>:<from>:<to> | remove:<t>:<tile> | undo:<t> | auto_layout:<t> | preset_save:<t>:<slot> | preset_load:<t>:<slot>
//   allhands:<t> | extinguish:<t> | clean:<t> | propane:<t> | promo:<t>          live verbs
//   auto:<t>:<0|1>                               Dispatcher / Head cook rules for a truck
//   research:<id>
//   ticket | cook:<grill|flip|assemble|serve>    the guide's hand-cooked first ticket
//   start_day | next_day | speed:<n> | pause | resume | open
using System;
using System.Collections.Generic;
using GrillConvoy.Sim;

namespace GrillConvoy.Game
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
        public int SelectedPitch { get; private set; } = -1;
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
        static ActionResult R(string err, double v = 0) => err == "" ? ActionResult.Done(v) : ActionResult.Fail(err);

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
            int t = Int(a, 1);
            switch (a[0])
            {
                case "select_truck":
                    if (t < 0 || t >= sim.S.Trucks.Count) return ActionResult.Fail("invalid");
                    SelectedTruck = t;
                    return ActionResult.Done(t);
                case "select_pitch":
                {
                    int node = sim.Gd.Node(a.Length > 1 ? a[1] : "");
                    if (node < 0 || !sim.Gd.Nodes[node].IsPitch) return ActionResult.Fail("invalid");
                    SelectedPitch = node;
                    return ActionResult.Done(node);
                }
                case "pitch":
                {
                    int node = sim.Gd.Node(a.Length > 2 ? a[2] : "");
                    if (node < 0) return ActionResult.Fail("invalid");
                    return Saved(sim.AssignPitch(t, node));
                }
                case "send":
                {
                    string e = sim.Send(t);
                    if (e == "") Analytics("truck_dispatch", new Dictionary<string, object> { { "pitch", sim.Gd.Nodes[sim.S.Trucks[t].Pitch].Id } });
                    return Saved(e);
                }
                case "restock": return Saved(sim.Restock(t));
                case "recall": return Saved(sim.Recall(t));
                case "load":
                {
                    int item = Stock.Index(a.Length > 2 ? a[2] : "");
                    int moved = sim.Load(t, item, Int(a, 3, 0), out string why);
                    return moved != 0 ? ActionResult.Done(moved) : ActionResult.Fail(why);
                }
                case "repack":
                    if (t < 0 || t >= sim.S.Trucks.Count) return ActionResult.Fail("invalid");
                    if (!sim.AtCommissary(sim.S.Trucks[t])) return ActionResult.Fail("away");
                    sim.Repack(t);
                    return ActionResult.Done();
                case "suggest_pack":
                    if (t < 0 || t >= sim.S.Trucks.Count) return ActionResult.Fail("invalid");
                    if (!sim.AtCommissary(sim.S.Trucks[t])) return ActionResult.Fail("away");
                    sim.ApplySuggestedPack(t);
                    return ActionResult.Done();
                case "prep":
                {
                    string e = sim.Prep(Stock.Index(a.Length > 1 ? a[1] : ""));
                    if (e == "") Analytics("prep_batch", new Dictionary<string, object> { { "item", a[1] } });
                    return Saved(e);
                }
                case "par": return R(sim.SetPar(Stock.Index(a.Length > 1 ? a[1] : ""), Int(a, 2, 0)));
                case "buy":
                {
                    string item = id.Substring(4);
                    string e = sim.Buy(item);
                    if (e == "") Analytics("sim_purchase", new Dictionary<string, object> { { "item", item.Split(':')[0] } });
                    return Saved(e);
                }
                case "role": return R(sim.SetRole(t, Int(a, 2), a.Length > 3 ? a[3] : ""));
                case "release": return Saved(sim.Release(t, Int(a, 2)));
                case "card": return R(sim.ToggleCard(t, a.Length > 2 ? a[2] : ""));
                case "tier": return R(sim.SetTier(t, Int(a, 2)));
                case "move":
                {
                    string e = sim.Move(t, Int(a, 2), Int(a, 3));
                    if (e == "") Analytics("layout_change", new Dictionary<string, object> { { "kind", "move" } });
                    return R(e);
                }
                case "remove": return Saved(sim.Remove(t, Int(a, 2)));
                case "undo": return R(sim.UndoLayout(t));
                case "auto_layout":
                {
                    string e = sim.AutoLayout(t);
                    if (e == "") Analytics("layout_change", new Dictionary<string, object> { { "kind", "auto" } });
                    return R(e);
                }
                case "preset_save": return Saved(sim.SavePreset(t, Int(a, 2)));
                case "preset_load": return R(sim.LoadPreset(t, Int(a, 2)));
                case "allhands": return R(sim.AllHands(t));
                case "extinguish": return R(sim.Extinguish(t));
                case "clean": return R(sim.Clean(t));
                case "propane": return R(sim.SwapPropane(t));
                case "promo": return R(sim.Promo(t));
                case "auto": return Saved(sim.SetAuto(t, Int(a, 2, 0) == 1));
                case "research":
                {
                    string rid = a.Length > 1 ? a[1] : "";
                    string e = sim.StartResearch(rid);
                    if (e == "") Analytics("research_start", new Dictionary<string, object> { { "id", rid } });
                    return Saved(e);
                }
                case "ticket": return R(sim.HandCook("ticket"));
                case "cook": return R(sim.HandCook(a.Length > 1 ? a[1] : ""));
                case "open":
                    return ActionResult.Done();
                case "start_day":
                    if (!sim.StartDay()) return ActionResult.Fail("phase");
                    sim.SetSpeed(Math.Max(1, Session.Settings.SpeedDefault));
                    Session.Save();
                    return ActionResult.Done();
                case "next_day":
                    if (!sim.NextDay()) return ActionResult.Fail("phase");
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
