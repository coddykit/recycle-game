// Volt City sim core — engine independent (no UnityEngine). C# 9 / .NET Standard 2.1.
using System.Collections.Generic;

namespace VoltCity.Sim
{
    public sealed class DayResult
    {
        public int Purchases;
        public long IdleCashEnd, NetLast, Income;      // Income = what the day actually earned (shifts + night crew)
    }

    /// <summary>
    /// Headless bot policies (balance runner, save fuzz, soak): operator (live shift) + buyer (between shifts) + the day model
    /// of DESIGN section 7 (30 min a day = 6 shifts, then one overnight away). Same public commands as a player.
    /// </summary>
    public static class Policies
    {
        public static readonly string[] All = { "greedy", "saver", "renewables_first", "gas_only", "battery_only", "never_shed", "naive_coach", "premium", "dispatcher" };
        static readonly string[] Renewable = { "hydro", "wind", "solar", "battery" };

        public static OperatorQ OperatorFor(string policy) => new OperatorQ
        {
            Setpoints = true, Battery = policy != "gas_only", Crew = true, Shed = policy != "never_shed", DemandResponse = true, Taps = 3
        };

        /// <summary>Plays one live shift to its end (the first shift is the guided one: TutorialSafe, as the game runs it).</summary>
        public static void PlayShift(Simulation sim, string policy, string weather = null, List<string> force = null)
        {
            sim.TutorialSafe = sim.S.ShiftNo == 0;
            if (!sim.StartShift(weather, force)) return;
            int every = policy == "naive_coach" ? 12 : 4;
            var q = OperatorFor(policy);
            // what the dispatcher assistant already does (owned + on) the bot leaves alone
            if (sim.S.Auto.Contains("baseload") && (!sim.S.AutoOn.TryGetValue("baseload", out bool b) || b)) q.Setpoints = false;
            if (sim.S.Auto.Contains("crew") && (!sim.S.AutoOn.TryGetValue("crew", out bool c) || c)) q.Crew = false;
            int guard = 0;
            while (sim.Phase == "live" && guard++ < 5000)
            {
                if (sim.Grid.Step % every == 0)
                {
                    var cmds = policy == "naive_coach" ? Dispatcher.Naive(sim) : Dispatcher.Operate(sim, q);
                    foreach (var cmd in cmds) sim.Queue(cmd);
                }
                sim.RunSteps(1);
            }
            sim.DrainEvents();
        }

        static bool IsRenewable(string t) => System.Array.IndexOf(Renewable, t) >= 0;

        static bool Allowed(Simulation sim, string policy, Buyable it)
        {
            if (it.Kind != "investment") return true;
            var inv = sim.Gd.Investment[it.Id];
            if (inv.Kind != "plant") return true;
            if (policy == "gas_only") return inv.Target == "gas";
            if (policy == "battery_only") return inv.Target == "battery";
            return true;
        }

        static bool RenewablePending(Simulation sim)
        {
            foreach (var x in sim.Gd.Investments)
                if (x.Kind == "plant" && IsRenewable(x.Target) && !sim.S.Owned.ContainsKey(x.Id) && Economy.WhyNot(sim, x.Id) == "cash") return true;
            return false;
        }

        static long NextHookupCost(Simulation sim)
        {
            foreach (var d in sim.Gd.Districts) if (!sim.S.IsHooked(d.Id)) return d.Hookup;
            return 0;
        }

        /// <summary>Buys until nothing more fits the policy. Returns the number of purchases.</summary>
        public static int BuyPhase(Simulation sim, string policy)
        {
            int n = 0;
            for (int guard = 0; guard < 80; guard++)
            {
                var list = Economy.BuyableNow(sim);
                Buyable pick = null;
                switch (policy)
                {
                    case "naive_coach":
                        // the coach's goal line: the next investment in the intended order, else the next district, else anything
                        foreach (var x in sim.Gd.Investments)
                        {
                            if (sim.S.Owned.ContainsKey(x.Id)) continue;
                            if (Economy.WhyNot(sim, x.Id) == "") pick = new Buyable { Kind = "investment", Id = x.Id, Cost = x.Cost };
                            break;
                        }
                        if (pick == null) foreach (var it in list) if (it.Kind == "hookup") { pick = it; break; }
                        if (pick == null && list.Count > 0) pick = list[0];
                        break;
                    case "renewables_first":
                        foreach (var it in list)
                            if (it.Kind == "investment" && sim.Gd.Investment[it.Id].Kind == "plant" && IsRenewable(sim.Gd.Investment[it.Id].Target)) { pick = it; break; }
                        if (pick == null)
                            foreach (var it in list)
                            {
                                if (it.Kind == "investment" && sim.Gd.Investment[it.Id].Kind == "plant" && RenewablePending(sim)) continue;
                                pick = it;
                                break;
                            }
                        break;
                    case "saver":
                        // hookups first; otherwise only buys while keeping half of the next hookup in reserve
                        foreach (var it in list) if (it.Kind == "hookup") { pick = it; break; }
                        if (pick == null)
                        {
                            long reserve = NextHookupCost(sim);
                            foreach (var it in list) if (sim.S.Cash - it.Cost >= reserve / 2) { pick = it; break; }
                        }
                        break;
                    default:
                        foreach (var it in list) if (Allowed(sim, policy, it)) { pick = it; break; }
                        break;
                }
                if (pick == null) break;
                bool ok = pick.Kind == "hookup" ? sim.Hookup(pick.Id) : sim.Buy(pick.Id);
                if (!ok) break;
                n++;
            }
            sim.DrainEvents();
            return n;
        }

        /// <summary>One day of the normal model: shifts (buying after each), then the overnight away (23.5 h) + the morning buy.</summary>
        public static DayResult PlayDay(Simulation sim, string policy, long nowUnix, int shifts = 6)
        {
            var r = new DayResult();
            string buyer = policy == "premium" || policy == "dispatcher" ? "greedy" : policy;
            string op = buyer;
            for (int k = 0; k < shifts; k++)
            {
                PlayShift(sim, op);
                r.Income += sim.Collect();
                r.Purchases += BuyPhase(sim, buyer);
            }
            r.IdleCashEnd = sim.S.Cash;
            sim.S.LastSeen = nowUnix;
            var off = sim.Offline(nowUnix + 84600);
            if (off != null) r.Income += off.Cash;
            r.Purchases += BuyPhase(sim, buyer);
            sim.DrainEvents();
            r.NetLast = sim.S.LastNet;
            return r;
        }

        /// <summary>New career for a policy (premium = premium + offline_plus + forecast_plus perks; dispatcher = the paid automation).</summary>
        public static Simulation NewCareer(GameData gd, string policy, long seed)
        {
            var sim = new Simulation(gd, seed);
            if (policy == "premium") sim.SetCommercePerks(new Dictionary<string, bool> { ["premium"] = true, ["offline_plus"] = true, ["forecast_plus"] = true });
            else if (policy == "dispatcher") sim.SetCommercePerks(new Dictionary<string, bool> { ["dispatcher"] = true });
            return sim;
        }
    }
}
