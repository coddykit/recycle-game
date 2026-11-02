// Grill Convoy sim core — headless balance bot (design §15 P1: 30 min / 2 h / 7 days). Plays like a sensible new player:
// sends each truck to the free pitch that sells the most burgers (crowd × group size, capped by its line), packs by the forecast, keeps the commissary prepped, restocks before selling
// out, puts out flare-ups, swaps propane, uses All hands when the line is long, hires, buys stations where the line is
// slowest (auto-arranged), researches and unlocks pitches when it can afford a buffer. Deterministic per seed.
using System;
using System.Collections.Generic;
using System.Text;

namespace GrillConvoy.Sim
{
    public sealed class BalanceReport
    {
        public readonly List<DayStats> Days = new List<DayStats>();
        public double Cash, Fame;
        public int Chapter, Trucks, ResearchDone, Stations;
        public double MinCash = double.MaxValue;
        public bool Finite = true;
        public readonly List<string> Log = new List<string>();

        public string Table()
        {
            var sb = new StringBuilder();
            sb.AppendLine("day weather  arr served lost%  burgers  wait sellout waste  sales  costs    net  stars  fame");
            foreach (var d in Days)
                sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "{0,3} {1,-7} {2,4} {3,6} {4,5:0.0} {5,8} {6,5:0.0} {7,7:0} {8,5} {9,6:0} {10,6:0} {11,6:0}   {12}/9",
                    d.Day, d.Weather, d.Arrivals, d.Served, d.LostRate * 100, d.Items, d.AvgWait, d.SelloutMin, d.Waste, d.Sales, d.Costs, d.Net, d.Stars));
            sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "cash {0:0} fame {1:0.0} chapter {2} trucks {3} stations {4} research {5} minCash {6:0}",
                Cash, Fame, Chapter, Trucks, Stations, ResearchDone, MinCash));
            return sb.ToString();
        }
    }

    public static class BalanceRunner
    {
        public static BalanceReport Run(GameData gd, long seed, int days, bool skipTutorial = true, Action<Simulation> eachDay = null)
        {
            var sim = new Simulation(gd, seed) { EmitCrowd = false };
            if (skipTutorial) sim.EndTutorial();
            var rep = new BalanceReport();
            while (sim.S.Day <= days)
            {
                eachDay?.Invoke(sim);
                PlayDay(sim, rep);
                rep.Days.Add(sim.S.History[sim.S.History.Count - 1]);
                if (sim.S.Day >= days) break;
                sim.NextDay();
            }
            rep.Cash = sim.S.Cash; rep.Fame = sim.S.Fame; rep.Chapter = sim.Chapter; rep.Trucks = sim.S.Trucks.Count;
            rep.ResearchDone = sim.S.Research.Count;
            foreach (var t in sim.S.Trucks) rep.Stations += t.Layout.Count;
            return rep;
        }

        public static void PlayDay(Simulation sim, BalanceReport rep)
        {
            Manage(sim, rep, true);
            sim.StartDay();
            int guard = 0;
            while (sim.Phase == Phases.Live && guard++ < 100000)
            {
                sim.AdvanceMinutes(5);
                sim.AdvanceResearch(5 / sim.Gd.GameMinPerRealS);   // research runs on real time: 5 game min = 2 s at 1×
                sim.DrainEvents();
                Manage(sim, rep, false);
                if (double.IsNaN(sim.S.Cash) || double.IsInfinity(sim.S.Cash)) rep.Finite = false;
                rep.MinCash = Math.Min(rep.MinCash, sim.S.Cash);
            }
            sim.DrainEvents();
        }

        static void Manage(Simulation sim, BalanceReport rep, bool morning)
        {
            var s = sim.S;
            if (morning) Morning(sim, rep);
            else Live(sim);
            // the commissary keeps one more day of patties / buns ready
            double need = 0;
            for (int ti = 0; ti < s.Trucks.Count; ti++) need += Math.Max(80, sim.SuggestPack(ti)[Stock.Patty]);
            for (int item = 0; item < Stock.N; item++)
            {
                double want = item == Stock.Side ? need * 0.4 : item == Stock.Bun ? need * 0.95 : need;
                if (item == Stock.Side && !HasSides(sim)) continue;
                int guard = 0;
                while (s.Commissary.Stock[item] + sim.Queued(item) < want && s.Cash > 300 && guard++ < 4)
                    if (sim.Prep(item) != "") break;
            }
        }

        static bool HasSides(Simulation sim)
        {
            foreach (var t in sim.S.Trucks) if (sim.Line(t).SideChance > 0) return true;
            return false;
        }

        static void Morning(Simulation sim, BalanceReport rep)
        {
            var s = sim.S;
            double buffer = Math.Max(600, sim.DayIncome() * 0.6);
            // pitches (the chapter goal), the second truck, research
            foreach (var d in sim.Gd.Districts)
                if (sim.CanBuy("pitch:" + d.Id).Ok && s.Cash - d.Cost > buffer) { sim.Buy("pitch:" + d.Id); rep.Log.Add("day " + s.Day + " pitch " + d.Id); }
            foreach (var v in sim.Gd.VehicleOrder)
                if (sim.CanBuy("truck:" + v).Ok && s.Cash - sim.CanBuy("truck:" + v).Cost > buffer) { sim.Buy("truck:" + v); rep.Log.Add("day " + s.Day + " truck " + v); }
            double baseBuffer = buffer;
            // saving up for the next pitch once Fame allows it (the chapter goal comes first)
            foreach (var d in sim.Gd.Districts)
                { var why = sim.CanBuy("pitch:" + d.Id).Reason; if (!sim.Unlocked(d.Id) && (why == "cash" || why == "")) { buffer += d.Cost; break; } }
            if (s.Researching == "")
                foreach (var r in Priority(sim))
                    if (sim.CanResearch(r) == "" && s.Cash - sim.Gd.ResearchById[r].Cost > buffer * 1.2) { sim.StartResearch(r); rep.Log.Add("day " + s.Day + " research " + r); break; }
            // each truck: the busiest pitch nobody else takes, crew, stations, menu, pack
            var taken = new HashSet<int>();
            for (int ti = 0; ti < s.Trucks.Count; ti++)
            {
                var t = s.Trucks[ti];
                int best = -1; double bestC = -1;
                foreach (int p in sim.Gd.Pitches)
                {
                    if (!sim.PitchOpen(p) || taken.Contains(p)) continue;
                    double c = PitchValue(sim, t, p);
                    if (c > bestC) { bestC = c; best = p; }
                }
                if (best >= 0) { taken.Add(best); sim.AssignPitch(ti, best); }
                for (int k = 0; k < 4 && sim.CanBuy("hire:" + ti + ":runner").Ok && s.Cash - sim.Gd.HirePrice > baseBuffer; k++)
                {
                    var L = sim.Line(t);
                    string role = t.Crew.Contains(Roles.Cashier) ? (L.Bottleneck == Stations.Grill ? Roles.Griller : L.Bottleneck == Stations.Assembly ? Roles.Assembler : Roles.Runner) : Roles.Cashier;
                    sim.Buy("hire:" + ti + ":" + role);
                }
                foreach (var st in new[] { Stations.Toaster, Stations.Fryer, Stations.Fridge, Stations.Sauce })
                    if (sim.CanBuy("station:" + ti + ":" + st).Ok && s.Cash - sim.Gd.StationDefs[st].Price > buffer * 1.3) { sim.Buy("station:" + ti + ":" + st); sim.AutoLayout(ti); }
                for (int k = 0; k < 2; k++)
                {
                    var L = sim.Line(t);
                    string st = L.Bottleneck;
                    var b = sim.CanBuy("station:" + ti + ":" + st);
                    if (b.Ok && s.Cash - b.Cost > baseBuffer * 1.2) { sim.Buy("station:" + ti + ":" + st); sim.AutoLayout(ti); rep.Log.Add("day " + s.Day + " station " + st); }
                }
                sim.AutoLayout(ti);
                foreach (var c in sim.Gd.Cards)
                    if (!t.Menu.Contains(c.Id) && sim.CardAvailable(c) && t.Menu.Count < sim.MenuSlots && c.Until == 0) sim.ToggleCard(ti, c.Id);
                sim.ApplySuggestedPack(ti);
            }
        }

        /// <summary>What a sensible player reads off the pitch card: the forecast crowd × what that crowd orders (match fans and
        /// beach families buy for the group, each type picks the card it likes best on this truck's board), capped by what the
        /// truck's line can make in a day, minus the permit. Dollars.</summary>
        static double PitchValue(Simulation sim, TruckState t, int p)
        {
            var n = sim.NodeAt(p);
            double per = 0, wsum = 0;
            for (int a = 0; a < n.Arch.Length && a < sim.Gd.Archetypes.Count; a++)
            {
                double price = 5, best = -1;
                foreach (var id in t.Menu)
                {
                    var c = sim.Gd.Card(id);
                    if (c != null && a < c.Appeal.Length && c.Appeal[a] > best) { best = c.Appeal[a]; price = c.Price; }
                }
                per += n.Arch[a] * sim.Gd.Archetypes[a].Mean * price;
                wsum += n.Arch[a] * sim.Gd.Archetypes[a].Mean;
            }
            double items = sim.ForecastCustomers(p) * (wsum > 0 ? wsum / Math.Max(1e-9, SumArch(n)) : 1);
            double avgPrice = wsum > 0 ? per / wsum : 5;
            double cap = sim.Line(t).Throughput * (sim.Gd.DayEnd - sim.Gd.DayStart) * 0.85;
            return Math.Min(items, cap) * avgPrice - n.Permit;
        }

        static double SumArch(NodeDef n)
        {
            double s = 0;
            foreach (var x in n.Arch) s += x;
            return s;
        }

        static IEnumerable<string> Priority(Simulation sim)
        {
            foreach (var id in new[] { "fryer", "card_double", "grill_plus", "drink_fridge", "fire_drill", "second_window", "commissary_plus", "crew_training",
                "par_levels", "order_ahead", "sauce_rail", "card_spicy", "menu_board_plus", "card_kids", "propane_gauge", "fuel_saver", "premium_tier",
                "dispatcher", "rival_scout", "card_veggie", "card_breakfast", "wrap_bench", "body_extension", "card_premium" })
                yield return id;
        }

        static void Live(Simulation sim)
        {
            var s = sim.S;
            for (int ti = 0; ti < s.Trucks.Count; ti++)
            {
                var t = s.Trucks[ti];
                if (t.Flaring) sim.Extinguish(ti);
                if (t.Propane <= sim.Gd.Svc.PropaneLow) sim.SwapPropane(ti);
                if (!t.Open) continue;
                if (t.Queue.Count >= 8) sim.AllHands(ti);
                if (t.Grease > sim.Gd.Svc.GreaseSafe && t.Queue.Count <= 2) sim.Clean(ti);
                bool low = t.Load[Stock.Patty] < 15 || t.Load[Stock.Bun] < 15;
                if (low && s.Commissary.Stock[Stock.Patty] >= 40 && s.Minute < sim.Gd.DayEnd - 90) sim.Restock(ti);
                if (sim.RivalMult(t.AtNode, null) < 1.0 && !t.Promo) sim.Promo(ti);
            }
        }
    }
}
