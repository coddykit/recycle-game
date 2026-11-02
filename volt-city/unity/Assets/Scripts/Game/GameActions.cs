// Volt City — every player action goes through ONE place (UI pills, world taps, coach, tests): GameActions.Run(id).
// Action ids (stable, used as coach / test targets too):
//   shift:start · collect · ramp:<plant>:<±1> · power:gas:<on|off> · bat:<mode> · dr · select:<fault> · send:<fault>[:<crew>]
//   · cancel:dispatch · shed:<district> · restore:<district> · buy:<investment> · hookup:<district> · speed:<n>
//   · open:<sheet> / card:<kind>:<id> (UI only: returned as Open) · shop:<product> · restore (purchases, UI/kit)
//   · P4: iap:<product> · look:<look> · policy (shop info strip) · reward:<placement>[:<fault>] · noop (an Owned pill)
// Sim commands are queued (applied at the next fixed step, SIM-API section 2); the result says when, so the UI can
// show "takes effect in 6 s" (LL Pit Lane Boss #133).
using System;
using System.Collections.Generic;
using VoltCity.Sim;

namespace VoltCity.Game
{
    public sealed class ActionResult
    {
        public bool Ok;
        /// <summary>Why not (sim / economy reason code) when !Ok.</summary>
        public string Reason = "";
        /// <summary>Toast text (already localised) or "".</summary>
        public string Toast = "";
        /// <summary>success | info | warning | danger.</summary>
        public string ToastKind = "info";
        /// <summary>UI navigation the action asks for ("report", "shop:dispatcher", "card:fault:3", "tab:crews", "close").</summary>
        public string Open = "";
        public long Cash;
    }

    /// <summary>Crew dispatch: tap a fault (spark or row) -> it is selected -> tap a truck (or "Send nearest") -> queued.</summary>
    public sealed class DispatchFlow
    {
        public int Fault { get; private set; } = -1;
        public bool Active => Fault >= 0;
        public void Select(int fault) => Fault = fault;
        public void Clear() => Fault = -1;

        /// <summary>Drops a selection that no longer makes sense (repaired, already assigned, shift over).</summary>
        public void Validate(Simulation sim)
        {
            if (Fault < 0) return;
            var f = sim.Grid?.FindFault(Fault);
            if (f == null || !f.Open || f.Crew >= 0) Fault = -1;
        }
    }

    public sealed class GameActions
    {
        readonly GameSession session;
        readonly Loc loc;
        public readonly DispatchFlow Dispatch = new DispatchFlow();
        /// <summary>P4 shop + rewarded (null in P3 tests: those actions fail with "unavailable").</summary>
        public Extras Extras;
        /// <summary>Observers (coach, analytics, hints): action id + result, after it ran.</summary>
        public event Action<string, ActionResult> Ran;

        public GameActions(GameSession session, Loc loc)
        {
            this.session = session;
            this.loc = loc;
        }

        Simulation Sim => session.Sim;

        static ActionResult Fail(string reason) => new ActionResult { Ok = false, Reason = reason };

        ActionResult Queue(Cmd cmd)
        {
            var q = Sim.Queue(cmd);
            return q.Ok ? new ActionResult { Ok = true } : Fail(q.Reason);
        }

        public ActionResult Run(string id)
        {
            var r = RunInner(id ?? "");
            Ran?.Invoke(id ?? "", r);
            return r;
        }

        ActionResult RunInner(string id)
        {
            var p = id.Split(':');
            var gd = Sim.Gd;
            switch (p[0])
            {
                case "shift":
                    if (p.Length > 1 && p[1] == "start")
                    {
                        Dispatch.Clear();
                        return session.StartShift() ? new ActionResult { Ok = true } : Fail("not_idle");
                    }
                    break;
                case "collect":
                {
                    if (Sim.Phase != "report") return Fail("not_report");
                    long cash = session.Collect();
                    return new ActionResult { Ok = true, Cash = cash, Open = "close", Toast = loc.F("toast.collected", "cash", Fmt.Money(cash, loc.Lang)), ToastKind = "success" };
                }
                case "ramp":
                {
                    if (p.Length < 3) break;
                    int dir = p[2].StartsWith("-") ? -1 : 1;
                    var g = Sim.Grid;
                    int pi = gd.PlantIndex(p[1]);
                    if (g == null || pi < 0 || pi > VoltCity.Sim.Grid.PHydro || g.PLevel[pi] == 0) return Fail("not_live");
                    int after = g.SetpointAfterPending(pi);
                    if ((dir > 0 && after >= 10) || (dir < 0 && after <= 0)) return Fail("limit");
                    var q = Sim.Queue(Cmd.Ramp(p[1], dir));
                    if (!q.Ok) return Fail(q.Reason);
                    string toast = q.EffectInS >= 2 ? loc.F("toast.ramp_in", "plant", Names.Plant(loc, p[1]), "s", Fmt.Dur(q.EffectInS, loc)) : "";
                    return new ActionResult { Ok = true, Toast = toast };
                }
                case "power":
                    return Queue(Cmd.Power(p.Length > 2 && p[2] == "on"));
                case "bat":
                    return p.Length > 1 ? Queue(Cmd.Battery(p[1])) : Fail("unknown");
                case "dr":
                {
                    var g = Sim.Grid;
                    if (g == null) return Fail("not_live");
                    if (g.Step < g.DrCdUntil) return Fail("cooldown");
                    return Queue(Cmd.DemandResponse());
                }
                case "select":
                {
                    if (p.Length < 2 || !int.TryParse(p[1], out int fid)) break;
                    var f = Sim.Grid?.FindFault(fid);
                    if (f == null || !f.Open) return Fail("no_fault");
                    if (f.Crew >= 0) return new ActionResult { Ok = true, Open = "card:fault:" + fid };
                    Dispatch.Select(fid);
                    return new ActionResult { Ok = true, Open = "card:fault:" + fid };
                }
                case "send":
                {
                    if (p.Length < 2 || !int.TryParse(p[1], out int fid)) break;
                    int crew = p.Length > 2 && int.TryParse(p[2], out int c) ? c : -1;
                    var g = Sim.Grid;
                    var f = g?.FindFault(fid);
                    if (f == null || !f.Open) return Fail("no_fault");
                    if (f.Crew >= 0) return Fail("assigned");
                    if (g.IdleCrews() == 0) return Fail("no_crew");
                    var r = Queue(Cmd.SendCrew(fid, crew));
                    if (r.Ok)
                    {
                        Dispatch.Clear();
                        r.Open = "close";
                    }
                    return r;
                }
                case "cancel":
                    Dispatch.Clear();
                    return new ActionResult { Ok = true };
                case "shed":
                    if (p.Length < 2) break;
                    if (gd.District.TryGetValue(p[1], out var dd) && dd.Backup) return Fail("hospital");
                    return Queue(Cmd.Shed(p[1]));
                case "restore":
                    if (p.Length < 2) return new ActionResult { Ok = true, Open = "restore_purchases" };
                    return Queue(Cmd.Restore(p[1]));
                case "buy":
                {
                    if (p.Length < 2) break;
                    string why = Economy.WhyNot(Sim, p[1]);
                    if (why != "") return Fail(why);
                    if (!session.Buy(p[1])) return Fail("unknown");
                    var x = gd.Investment[p[1]];
                    return new ActionResult { Ok = true, Toast = loc.F("toast.bought", "name", Names.Investment(loc, gd, x)), ToastKind = "success" };
                }
                case "hookup":
                {
                    if (p.Length < 2) break;
                    string why = Economy.HookupWhyNot(Sim, p[1]);
                    if (why != "") return Fail(why);
                    if (!session.Hookup(p[1])) return Fail("unknown");
                    return new ActionResult { Ok = true, Toast = loc.F("toast.hooked", "district", Names.District(loc, p[1])), ToastKind = "success" };
                }
                case "speed":
                {
                    if (p.Length < 2 || !int.TryParse(p[1], out int x)) break;
                    if (!Sim.SetSpeed(x)) return Fail(x == gd.Speed4 ? "speed_locked" : "unknown");
                    session.Settings.SpeedDefault = x;
                    return new ActionResult { Ok = true };
                }
                case "speed_next":
                {
                    // the HUD speed pill cycles 1 -> 2 -> 3 (-> 4 with the perk) -> 1
                    int cur = Sim.S.Speed, next = 1;
                    var opts = new List<int>(gd.Speeds);
                    if (Sim.Perk("speed4")) opts.Add(gd.Speed4);
                    int k = opts.IndexOf(cur);
                    next = opts[(k + 1) % opts.Count];
                    Sim.SetSpeed(next);
                    session.Settings.SpeedDefault = next;
                    return new ActionResult { Ok = true };
                }
                case "shop":
                    if (p.Length > 1 && p[1] == "retry")
                    {
                        Extras?.Shop?.LoadPrices();
                        return new ActionResult { Ok = true };
                    }
                    return new ActionResult { Ok = true, Open = id };
                case "iap":
                    if (p.Length < 2 || Extras?.Shop == null) return Fail("unavailable");
                    return Extras.Shop.Buy(p[1]) ? new ActionResult { Ok = true } : Fail(Extras.Shop.WhyNot(p[1]) == "owned" ? "owned" : "unavailable");
                case "look":
                    if (p.Length < 2 || Extras?.Shop == null) return Fail("unavailable");
                    return Extras.Shop.ToggleLook(p[1]) ? new ActionResult { Ok = true } : Fail("unavailable");
                case "policy":
                    if (Extras?.Shop == null) return Fail("unavailable");
                    Extras.Shop.TogglePolicy();
                    return new ActionResult { Ok = true };
                case "reward":
                {
                    if (p.Length < 2 || Extras?.Rewards == null) return Fail("unavailable");
                    int fault = p.Length > 2 && int.TryParse(p[2], out int fv) ? fv : -1;
                    return Extras.Rewards.Claim(p[1], fault) ? new ActionResult { Ok = true } : Fail("reward_unavailable");
                }
                case "noop":
                    return new ActionResult { Ok = true };
                case "open":
                case "card":
                    if (p.Length < 2) break;
                    return new ActionResult { Ok = true, Open = p[0] == "card" ? id : id.Substring(5) };
            }
            return Fail("unknown");
        }
    }
}
