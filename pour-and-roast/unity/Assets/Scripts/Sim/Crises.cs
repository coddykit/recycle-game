// Pour & Roast sim core — weather + crises (design §7, MVP 6: rain, heat, missed milk delivery, grinder jam, frost at
// origin, bad-review spree). Short, telegraphed, never game over, never in the guide or the first free days:
//   * weather comes from the forecast strip (known a day ahead; the forecast board shows 3 days) — rain empties the
//     benches and stacks pre-orders, heat floods the rail with iced drinks, fog thins walk-ins;
//   * milk_late is rolled each morning by the supplier tier and shown in the plan (the van comes 3 h late);
//   * grinder_jam comes from wear (Bar.Wear), review_spree from low reviews in a row (Bar.PostReview);
//   * frost is the only random roll (green market, roastery chapter): one origin ×1.5 for 3 days.
using System;
using System.Collections.Generic;

namespace PourAndRoast.Sim
{
    public sealed partial class Simulation
    {
        /// <summary>Deterministic weather of a day: the scripted first days (the guide's "rain tomorrow"), then seeded rolls
        /// (no heat before the free days end).</summary>
        string ForecastFor(int day)
        {
            if (day >= 1 && day <= Gd.StartForecast.Count) return Gd.StartForecast[day - 1];
            var rng = Rng.Stream(S.Seed, 7919L * day + 13);
            var w = new double[Weathers.All.Length];
            for (int i = 0; i < w.Length; i++)
            {
                string id = Weathers.All[i];
                w[i] = Gd.Weather.TryGetValue(id, out var d) ? d.Weight : 0;
                var ev = Gd.Events.Find(e => e.Id == id);
                if (ev != null && day < ev.MinDay) w[i] = 0;
            }
            int k = rng.PickWeighted(w);
            return k < 0 ? Weathers.Sunny : Weathers.All[k];
        }

        void PlanEvents()
        {
            if (!CrisesAllowed) return;
            foreach (var e in Gd.Events)
            {
                if (e.Trigger != "market" || S.Day < e.MinDay || !RoasteryOpen) continue;
                if (S.Events.Exists(a => a.Id == e.Id)) continue;
                if (Rng.Frand() < e.Weight) Fire(e.Id);
            }
        }

        EventDef Def(string id) => Gd.Events.Find(e => e.Id == id) ?? new EventDef { Id = id, Mult = 1, Days = 1 };

        /// <summary>Applies a crisis now (also used by tests and the balance bot). Returns false when it found no target.</summary>
        public bool Fire(string id)
        {
            var e = Def(id);
            switch (id)
            {
                case "frost":
                {
                    if (Gd.Origins.Count == 0) return false;
                    int o = Rng.RangeI(0, Gd.Origins.Count - 1);
                    S.Events.Add(new ActiveEvent { Id = id, UntilDay = S.Day + Math.Max(1, e.Days) - 1, Mult = e.Mult, Target = o });
                    Emit("crisis", o, -1, e.Mult, id);
                    return true;
                }
                case "review_spree":
                    // footfall drops for the rest of today and tomorrow
                    S.Events.Add(new ActiveEvent { Id = id, UntilDay = S.Day + 1, Mult = e.Mult });
                    Emit("crisis", -1, -1, e.Mult, id);
                    return true;
                case "grinder_jam":
                    return Jam("crisis");
                case "milk_late":
                    if (S.Phase != Phases.Live && S.Phase != Phases.Plan) return false;
                    S.MilkArriveAt = Math.Max(S.Minute, Gd.DayStart) + Gd.MilkLateMin;
                    Emit("crisis", -1, -1, Gd.MilkLateMin, id);
                    return true;
                case "rain":
                case "heat":
                    if (S.Forecast.Count == 0) S.Forecast.Add(id); else S.Forecast[0] = id;
                    S.Today.Weather = id;
                    Emit("crisis", -1, -1, 0, id);
                    return true;
            }
            return false;
        }

        /// <summary>Crises active now (for the plan card and the HUD chips).</summary>
        public List<string> ActiveCrises()
        {
            var o = new List<string>();
            if (Weather == Weathers.Rain || Weather == Weathers.Heat) o.Add(Weather);
            if (S.MilkArriveAt > S.Minute && S.MilkArriveAt > Gd.DayStart) o.Add("milk_late");
            if (Jammed) o.Add("grinder_jam");
            foreach (var e in S.Events) if (!o.Contains(e.Id)) o.Add(e.Id);
            return o;
        }
    }
}
