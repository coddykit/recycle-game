// Grill Convoy sim core — the commissary kitchen (design §4 loop 3, §5.7): prep batches (patties, buns, sides) cost money
// when queued and take game time (one at a time; the night crew works through the queue after the day ends); the pack list
// moves stock from the commissary fridge onto a parked truck. Under-load = sell-out at the pitch; over-load = leftovers that
// partly spoil at night (waste). "Pack like yesterday" reloads the last departure load; the forecast suggestion sizes it.
using System;
using System.Collections.Generic;

namespace GrillConvoy.Sim
{
    public sealed partial class Simulation
    {
        public const int MaxPrepQueue = 4;

        public int BatchSize(int item) => (int)Math.Round(Gd.StockDefs[item].Batch * (Has("commissary_plus") ? 1.5 : 1.0));
        public double BatchCost(int item) => Gd.StockDefs[item].Cost * (Has("commissary_plus") ? 1.5 : 1.0);
        public double PrepSpeed => Has("commissary_plus") ? 1.3 : 1.0;
        public int Queued(int item) { int n = 0; foreach (var q in S.Commissary.Queue) if (Stock.Index(q) == item) n += BatchSize(item); return n; }
        public int TruckCap(TruckState t, int item) => VehicleOf(t).Cap[item];
        public bool AtCommissary(TruckState t) => t.AtNode == Gd.DepotNode && (t.Status == TruckStatus.Idle || t.Status == TruckStatus.Restock);

        /// <summary>Queues one prep batch (paid now). "" = ok.</summary>
        public string Prep(int item)
        {
            if (item < 0 || item >= Stock.N) return "invalid";
            if (S.Phase == Phases.Report) return "report";
            var c = S.Commissary;
            if (c.Queue.Count >= MaxPrepQueue) return "queue_full";
            if (c.Stock[item] + Queued(item) + BatchSize(item) > Gd.StockDefs[item].Cap) return "fridge_full";
            double cost = BatchCost(item);
            if (S.Cash < cost) return "cash";
            S.Cash -= cost;
            S.Today.Ingredients += cost;
            if (c.Queue.Count == 0) c.PrepLeft = Gd.StockDefs[item].PrepMin;
            c.Queue.Add(Stock.Ids[item]);
            Emit("prep", item, BatchSize(item), cost);
            return "";
        }

        void StepCommissary(double dt)
        {
            var c = S.Commissary;
            double left = dt * PrepSpeed;
            int guard = 0;
            while (c.Queue.Count > 0 && left > 1e-9 && guard++ < 10)
            {
                double use = Math.Min(left, c.PrepLeft);
                c.PrepLeft -= use;
                left -= use;
                if (c.PrepLeft <= 1e-9) FinishBatch();
            }
        }

        /// <summary>The head batch is done now (the prep timer, or the rewarded Quick Prep).</summary>
        void FinishBatch()
        {
            var c = S.Commissary;
            if (c.Queue.Count == 0) return;
            int item = Math.Max(0, Stock.Index(c.Queue[0]));
            c.Queue.RemoveAt(0);
            c.Stock[item] = Math.Min(Gd.StockDefs[item].Cap, c.Stock[item] + BatchSize(item));
            c.PrepLeft = c.Queue.Count > 0 ? Gd.StockDefs[Math.Max(0, Stock.Index(c.Queue[0]))].PrepMin : 0;
            Emit("prep_done", item, BatchSize(item));
        }

        /// <summary>Moves stock between the commissary and a parked truck (delta &lt; 0 = back to the fridge). Returns the
        /// amount moved (signed) or 0; why = reason when nothing moved.</summary>
        public int Load(int truck, int item, int delta, out string why)
        {
            why = "";
            if (truck < 0 || truck >= S.Trucks.Count || item < 0 || item >= Stock.N || delta == 0) { why = "invalid"; return 0; }
            var t = S.Trucks[truck];
            if (!AtCommissary(t)) { why = "away"; return 0; }
            var c = S.Commissary;
            int moved;
            if (delta > 0)
            {
                moved = Math.Min(delta, Math.Min(c.Stock[item], TruckCap(t, item) - t.Load[item]));
                if (moved <= 0) { why = c.Stock[item] <= 0 ? "no_stock" : "truck_full"; return 0; }
            }
            else
            {
                moved = -Math.Min(-delta, t.Load[item]);
                if (moved == 0) { why = "empty"; return 0; }
            }
            t.Load[item] += moved;
            c.Stock[item] -= moved;
            t.Pack[item] = t.Load[item];
            return moved;
        }

        /// <summary>Tops the truck up to its pack list from the commissary (as far as stock allows).</summary>
        public void Repack(int truck, bool keepPack = true)
        {
            if (truck < 0 || truck >= S.Trucks.Count) return;
            var t = S.Trucks[truck];
            var want = (int[])t.Pack.Clone();
            for (int i = 0; i < Stock.N; i++)
            {
                int need = Math.Min(want[i], TruckCap(t, i)) - t.Load[i];
                if (need <= 0) continue;
                int got = Math.Min(need, S.Commissary.Stock[i]);
                t.Load[i] += got;
                S.Commissary.Stock[i] -= got;
            }
            if (keepPack) for (int i = 0; i < Stock.N; i++) t.Pack[i] = want[i];
        }

        /// <summary>Forecast-based pack list for a truck at a pitch (customers × burgers each × 1.1, capped).</summary>
        public int[] SuggestPack(int truck, bool tomorrow = false)
        {
            var r = new int[Stock.N];
            if (truck < 0 || truck >= S.Trucks.Count) return r;
            var t = S.Trucks[truck];
            if (t.Pitch < 0) return r;
            var L = Line(t);
            double customers = ForecastCustomers(t.Pitch, tomorrow) * 0.85 / Math.Max(1, TrucksAssigned(t.Pitch));
            customers = Math.Min(customers, L.Throughput * Gd.DayMinutes * 0.85 / Math.Max(0.5, AverageItems(t.Pitch) / 100.0));
            double items = customers * AverageItems(t.Pitch) / 100.0 * 1.05;
            double patties = items * 1.05;
            r[Stock.Patty] = Math.Min(TruckCap(t, Stock.Patty), (int)Math.Ceiling(patties / 10.0) * 10);
            r[Stock.Bun] = Math.Min(TruckCap(t, Stock.Bun), (int)Math.Ceiling(items / 10.0) * 10);
            r[Stock.Side] = L.SideChance > 0 ? Math.Min(TruckCap(t, Stock.Side), (int)Math.Ceiling(customers * 0.45 * L.SideChance / 10.0) * 10) : 0;
            return r;
        }

        /// <summary>A restock run loads what the rest of the day needs at its pitch (forecast × 1.15), at most the pack list.</summary>
        void RestockLoad(int truck)
        {
            var t = S.Trucks[truck];
            if (t.Pitch < 0) { Repack(truck); return; }
            var n = NodeAt(t.Pitch);
            var c = Gd.Curve(n.Curve);
            double share = 0, total = 0;
            for (int h = 0; h < c.Length; h++) { total += c[h]; if (Gd.DayStart + (h + 1) * 60 > S.Minute + 20) share += c[h] * Math.Min(1, (Gd.DayStart + (h + 1) * 60 - S.Minute - 20) / 60.0); }
            double frac = total <= 0 ? 1 : Math.Min(1, share / total * 1.15 + 0.05);
            var want = SuggestPack(truck);
            var keep = (int[])t.Pack.Clone();
            for (int i = 0; i < Stock.N; i++) t.Pack[i] = Math.Min(keep[i], Math.Max(10, (int)Math.Ceiling(Math.Max(want[i], keep[i]) * frac / 10.0) * 10));
            Repack(truck, false);
            for (int i = 0; i < Stock.N; i++) t.Pack[i] = keep[i];
        }

        public int TrucksAssigned(int node) { int n = 0; foreach (var t in S.Trucks) if (t.Pitch == node) n++; return n; }

        /// <summary>Sets the pack list to the suggestion and loads what the fridge has.</summary>
        public void ApplySuggestedPack(int truck, bool tomorrow = false)
        {
            var t = S.Trucks[truck];
            if (!AtCommissary(t)) return;
            var want = SuggestPack(truck, tomorrow);
            for (int i = 0; i < Stock.N; i++)
            {
                int delta = want[i] - t.Load[i];
                if (delta != 0) Load(truck, i, delta, out _);
                t.Pack[i] = want[i];
            }
        }

        /// <summary>Leftovers back into the commissary; at night patties / sides partly spoil (waste).</summary>
        void UnloadToCommissary(TruckState t, bool night)
        {
            var c = S.Commissary;
            for (int i = 0; i < Stock.N; i++)
            {
                int n = t.Load[i];
                if (n <= 0) continue;
                int spoiled = night ? (int)Math.Round(n * Gd.StockDefs[i].Spoil) : 0;
                if (spoiled > 0)
                {
                    S.Today.Waste += spoiled;
                    S.Today.WasteValue += spoiled * Gd.StockDefs[i].Cost / Math.Max(1, Gd.StockDefs[i].Batch);
                }
                c.Stock[i] = Math.Min(Gd.StockDefs[i].Cap, c.Stock[i] + n - spoiled);
                t.Load[i] = 0;
            }
        }

        /// <summary>Par levels (research): the night shift queues batches up to the par of each item (paid).</summary>
        void AutoPrep()
        {
            if (!Has("par_levels")) return;
            var c = S.Commissary;
            for (int pass = 0; pass < MaxPrepQueue; pass++)
                for (int i = 0; i < Stock.N; i++)
                    if (c.Stock[i] + Queued(i) < c.Par[i]) Prep(i);
        }

        public string SetPar(int item, int value)
        {
            if (item < 0 || item >= Stock.N) return "invalid";
            S.Commissary.Par[item] = Math.Max(0, Math.Min(Gd.StockDefs[item].Cap, value));
            return "";
        }
    }
}
