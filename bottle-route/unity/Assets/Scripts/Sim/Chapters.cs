// Bottle Route sim core — the mechanics of chapters 5-7 (plan §6), all data-driven from balance.json / map.json:
//   * ch5 Industrial Estate: bale TENDERS (an offer to deliver N bales of a material by day D at a fixed premium price;
//     accept / decline; at most max_open at a time; no penalty beyond losing the premium) and the BULK COUNTING CENTRE
//     depot upgrade (extra count throughput, Economy.cs / Depot.cs).
//   * ch6 Riviera Resort: the SUMMER SEASON — every `every_days` days a `days`-long window in which the season districts'
//     points get ×mult arrivals (in the estimate too: the season is on the calendar) and heat waves are likelier. Start and
//     end are announced in the morning (season_start / season_end events).
//   * ch7 Hill Villages + Island: long roads (fuel), large remote machines (point kind machine_l) and the FERRY — an edge
//     flag in the road graph; a truck waits at the pier for the next departure (every `every_min` game minutes from the
//     day start), pays a ticket, sails at the ferry speed and pays no fuel on the water (Fleet.cs).
// Every RNG draw here happens only once the chapter's district is open, so chapters 1-4 replay exactly as before.
using System;
using System.Collections.Generic;

namespace BottleRoute.Sim
{
    public sealed partial class Simulation
    {
        // ------------------------------------------------------------------------------------------------ season

        /// <summary>Is day d inside the summer season window (calendar only)?</summary>
        public bool SeasonWindow(int day)
        {
            if (Gd.SeasonDays <= 0 || Gd.SeasonEvery <= 0) return false;
            int k = ((day - Gd.SeasonStart) % Gd.SeasonEvery + Gd.SeasonEvery) % Gd.SeasonEvery;
            return day >= Gd.SeasonStart && k < Gd.SeasonDays;
        }

        /// <summary>The season runs today: inside the window and at least one season district is open.</summary>
        public bool InSeason
        {
            get
            {
                if (!SeasonWindow(S.Day)) return false;
                foreach (var d in Gd.SeasonDistricts) if (Unlocked(d)) return true;
                return false;
            }
        }

        /// <summary>Days left in the current season (incl. today), or days until the next one starts when outside.</summary>
        public int SeasonDaysLeft
        {
            get
            {
                int n = 0;
                bool now = SeasonWindow(S.Day);
                for (int d = S.Day; n < Gd.SeasonEvery + 1 && SeasonWindow(d) == now; d++) n++;
                return n;
            }
        }

        /// <summary>Arrival multiplier of a point from the season (1 outside it or outside the season districts).</summary>
        public double SeasonMult(PointState p)
        {
            if (Gd.SeasonDistricts.Count == 0 || !Gd.SeasonDistricts.Contains(NodeOf(p).District)) return 1.0;
            return InSeason ? Gd.SeasonMult : 1.0;
        }

        /// <summary>Morning: announce a season start / end (no RNG).</summary>
        void UpdateSeason()
        {
            bool on = InSeason;
            if (on == S.SeasonOn) return;
            S.SeasonOn = on;
            Emit(on ? "season_start" : "season_end", S.Day, -1, Gd.SeasonMult);
        }

        double EventWeight(EventDef e) => e.Id == "heat_wave" && InSeason ? e.Weight * Gd.SeasonHeatWeight : e.Weight;

        // ------------------------------------------------------------------------------------------------ tenders

        public bool TendersOpen => Unlocked(Gd.TenderDistrict);

        public Tender TenderById(int id) => S.Tenders.Find(t => t.Id == id);

        /// <summary>Premium of a tender over today's spot price (1.3 = 30 % more).</summary>
        public double TenderPremium(Tender t) => t.Price / Math.Max(1, BalePrice(t.Mat));

        /// <summary>A new offer (the morning draw; force = tests and the guide skip the chance roll). Null when none.</summary>
        public Tender OfferTender(bool force = false)
        {
            if (!TendersOpen || S.Tutorial) return null;
            if (S.Tenders.Count >= Math.Max(0, Gd.TenderMaxOpen)) return null;
            if (!force && Rng.Frand() >= Gd.TenderChance) return null;
            int m = Rng.PickWeighted(Gd.TenderWeights);
            if (m < 0 || m >= Mats.N) m = Mats.Pet;
            var range = Gd.TenderBales[m];
            int cap = m == Mats.Glass ? GlassBayCap : Math.Max(2, S.Depot.YardCap - 2);
            int n = Math.Max(1, Math.Min(cap, Rng.RangeI(range[0], range[1])));
            double premium = Gd.TenderPremiumMin + Rng.Frand() * (Gd.TenderPremiumMax - Gd.TenderPremiumMin);
            var t = new Tender
            {
                Id = S.NextTenderId++, Mat = m, Bales = n, Price = Math.Round(Gd.Materials[m].BalePrice * premium),
                OfferUntil = S.Day + Math.Max(1, Gd.TenderOfferDays) - 1,
                DueDay = S.Day + Rng.RangeI(Math.Max(1, Gd.TenderDueMin), Math.Max(Gd.TenderDueMin, Gd.TenderDueMax)),
            };
            S.Tenders.Add(t);
            Emit("tender_offer", t.Id, m, n);
            return t;
        }

        /// <summary>Morning: offers that were not accepted lapse, accepted ones past their day run out (no fine).</summary>
        void ExpireTenders()
        {
            for (int i = S.Tenders.Count - 1; i >= 0; i--)
            {
                var t = S.Tenders[i];
                if (!t.Accepted && t.OfferUntil < S.Day) { S.Tenders.RemoveAt(i); Emit("tender_lapsed", t.Id, t.Mat, t.Bales); }
                else if (t.Accepted && t.DueDay < S.Day) { S.Tenders.RemoveAt(i); Emit("tender_expired", t.Id, t.Mat, t.Bales); }
            }
        }

        public string AcceptTender(int id)
        {
            var t = TenderById(id);
            if (t == null) return "invalid";
            if (t.Accepted) return "owned";
            t.Accepted = true;
            Emit("tender_accepted", t.Id, t.Mat, t.Bales);
            return "";
        }

        public string DeclineTender(int id)
        {
            var t = TenderById(id);
            if (t == null) return "invalid";
            S.Tenders.Remove(t);
            Emit("tender_declined", t.Id, t.Mat, t.Bales);
            return "";
        }

        public bool CanDeliver(Tender t) => t != null && t.Accepted && S.Depot.Bales[t.Mat] >= t.Bales;

        /// <summary>Hands the tender's bales over and is paid the tender price. "" = ok.</summary>
        public string DeliverTender(int id)
        {
            var t = TenderById(id);
            if (t == null) return "invalid";
            if (!t.Accepted) return "phase";
            if (S.Depot.Bales[t.Mat] < t.Bales) return "no_bales";
            double amount = Math.Round(t.Bales * t.Price);
            S.Depot.Bales[t.Mat] -= t.Bales;
            S.Cash += amount;
            S.Today.Sales += amount;
            S.Tenders.Remove(t);
            Emit("tender_done", t.Mat, t.Bales, amount);
            return "";
        }

        /// <summary>Day end: accepted tenders that the yard can fill are delivered by themselves (nobody loses one for not
        /// tapping in time).</summary>
        void AutoDeliverTenders()
        {
            for (int i = S.Tenders.Count - 1; i >= 0; i--)
            {
                if (i >= S.Tenders.Count) continue;
                var t = S.Tenders[i];
                if (CanDeliver(t)) DeliverTender(t.Id);
            }
        }

        /// <summary>Bales held for accepted tenders of material m (the market sheet and the bot keep them out of spot sales).</summary>
        public int BalesPromised(int m)
        {
            int n = 0;
            foreach (var t in S.Tenders) if (t.Accepted && t.Mat == m) n += t.Bales;
            return n;
        }

        // ------------------------------------------------------------------------------------------------ ferry

        /// <summary>The first ferry departure at or after day minute m.</summary>
        public double NextFerry(double m)
        {
            double every = Math.Max(1, Gd.FerryEveryMin);
            double k = Math.Ceiling((m - Gd.DayStart) / every - 1e-9);
            return Gd.DayStart + Math.Max(0, k) * every;
        }

        /// <summary>The truck waits at a pier for the next departure.</summary>
        public bool AtPier(TruckState t) => t.Status == TruckStatus.Drive && t.FerryAt > 0 && S.Minute < t.FerryAt - 1e-9;
        /// <summary>The truck is on the boat.</summary>
        public bool OnFerry(TruckState t) => t.Status == TruckStatus.Drive && t.FerryAt > 0 && S.Minute >= t.FerryAt - 1e-9;

        /// <summary>The first ferry segment on the truck's path that ends after its position: segment start / end km.</summary>
        bool NextFerrySegment(TruckState t, out double start, out double end) => NextFerrySegment(t, out start, out end, out _);

        bool NextFerrySegment(TruckState t, out double start, out double end, out int pier)
        {
            start = end = 0;
            pier = -1;
            if (!Gd.Graph.HasFerry || t.Path.Count < 2) return false;
            double km = 0;
            for (int i = 1; i < t.Path.Count; i++)
            {
                var a = Gd.Nodes[t.Path[i - 1]]; var b = Gd.Nodes[t.Path[i]];
                double seg = Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y)) * Gd.RoadFactor;
                if (Gd.Graph.IsFerry(t.Path[i - 1], t.Path[i]) && km + seg > t.PathPos + 1e-6) { start = km; end = km + seg; pier = t.Path[i - 1]; return true; }
                km += seg;
            }
            return false;
        }

        /// <summary>Road km (fuel) still ahead on the current leg.</summary>
        double RoadKmLeft(TruckState t)
        {
            double left = Math.Max(0, t.PathKm - t.PathPos);
            if (!Gd.Graph.HasFerry || t.Path.Count < 2) return left;
            double km = 0;
            for (int i = 1; i < t.Path.Count; i++)
            {
                var a = Gd.Nodes[t.Path[i - 1]]; var b = Gd.Nodes[t.Path[i]];
                double seg = Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y)) * Gd.RoadFactor;
                if (Gd.Graph.IsFerry(t.Path[i - 1], t.Path[i]))
                {
                    double from = Math.Max(km, t.PathPos), to = km + seg;
                    if (to > from) left -= to - from;
                }
                km += seg;
            }
            return Math.Max(0, left);
        }

        void PayFerry(TruckState t)
        {
            double c = Gd.FerryTicket;
            if (c <= 0) return;
            S.Cash -= c;
            S.Today.Fuel += c;   // the report's fuel line = fuel + ferry tickets
        }
    }
}
