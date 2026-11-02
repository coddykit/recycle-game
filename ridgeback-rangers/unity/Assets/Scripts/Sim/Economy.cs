// Ridgeback Rangers sim core — the day cycle, the economy, purchases (instant: no builder timers, plan §3 originality),
// offline pay and the sim side of rewarded placements (caps + cooldowns persisted in the career; the kit's
// RewardedController enforces its own too).
using System;
using System.Collections.Generic;

namespace RidgebackRangers.Sim
{
    public sealed class OfflineSummary { public double Seconds, Hours, Amount; public bool Capped; }
    public sealed class RewardOfferInfo { public bool Ok; public double Amount; public string Reason = ""; }
    public sealed class RewardResult { public bool Ok; public double Amount; public string Reason = ""; }
    public sealed class BuyInfo { public bool Ok; public double Cost; public string Reason = ""; }

    public sealed partial class Simulation
    {
        // ------------------------------------------------------------------------------------------------ day cycle

        /// <summary>Plan -> live (08:00): the feed board's crates go out, the gate opens (after the guide).</summary>
        public bool StartDay()
        {
            if (S.Phase != Phases.Plan) return false;
            S.Phase = Phases.Live;
            S.Paused = false;
            if (!S.Tutorial) S.GateOpen = true;
            if (!(S.Tutorial && S.Day == 1)) DeliverFeed(false);   // day 1 of the guide: the Feeder is the lesson
            S.Tram.BoardSince = S.Minute;
            Emit("day_live", S.Day);
            return true;
        }

        /// <summary>The gate (guide step: open it by hand; afterwards it opens every morning).</summary>
        public string OpenGate()
        {
            if (S.Phase != Phases.Live) return "phase";
            if (S.GateOpen) return "open";
            S.GateOpen = true;
            Emit("gate", 1);
            return "";
        }

        void EndDay()
        {
            S.Minute = Gd.DayEnd;
            // the last riders finish their loop, the queue walks the paths
            if (S.Tram.Running || S.Tram.Riders > 0) Arrive();
            foreach (var g in S.Queue) Walk(g.N);
            S.Queue.Clear();
            CloseDayPins();
            SampleCare();
            var td = S.Today;
            foreach (var r in S.Rangers) td.Wages += Gd.RoleDefs.TryGetValue(r.Role, out var d) ? d.Wage : 0;
            td.Upkeep = S.Paddocks.Count * Gd.PaddockUpkeep + Gd.TramUpkeep + S.Tram.Cars * Gd.TramUpkeepCar;
            S.Cash -= td.Wages + td.Upkeep;
            double cpCare = Math.Round(td.Care * Gd.CpPerCare);
            td.CpEarned += cpCare;
            S.Cp += cpCare;
            td.StarCare = StarsAbove(td.Care, Gd.StarCare);
            td.StarVisitors = td.SatN <= 0 ? 0 : StarsAbove(td.Satisfaction, Gd.StarVisitors);
            td.StarCalm = StarsAbove(td.Calm, Gd.StarCalm);
            double score = td.Stars / 9.0 * 100.0;
            double next = S.Rating + Gd.RatingAlpha * (score - S.Rating);
            // forgiving start: the guide and the first days can only raise the rating
            if (S.Tutorial || S.Day <= Gd.RatingFreeDays) next = Math.Max(S.Rating, next);
            S.Rating = Math.Max(0, Math.Min(100, next));
            if (td.Stars > S.BestDayStars) S.BestDayStars = td.Stars;
            if (td.Stars >= 8) S.ThreeStarDays++;
            S.History.Add(td);
            while (S.History.Count > 14) S.History.RemoveAt(0);
            S.Tram.Running = false; S.Tram.Pos = 0; S.Tram.NextStop = 1; S.Tram.Dwell = 0;
            S.Events.RemoveAll(e => e.Until > 0);
            S.Phase = Phases.Report;
            Emit("day_done", S.Day, td.Stars, td.Net);
        }

        static int StarsAbove(double x, double[] th)
        {
            int s = 0;
            for (int i = 0; i < th.Length; i++) if (x >= th[i] - 1e-12) s = i + 1;
            return s;
        }

        /// <summary>Report -> next morning's plan.</summary>
        public bool NextDay()
        {
            if (S.Phase != Phases.Report) return false;
            S.Day++;
            S.Minute = Gd.DayStart;
            S.Today = new DayStats { Day = S.Day };
            S.Events.RemoveAll(e => e.UntilDay < S.Day);
            GrowUp();
            PlanDay();
            S.Phase = Phases.Plan;
            S.Paused = false;
            Emit("day_start", S.Day);
            return true;
        }

        // ------------------------------------------------------------------------------------------------ money

        /// <summary>Gross income of a usual day (avg of the last 3 reports, floor) — the unit of packs and rewards.</summary>
        public double DayIncome()
        {
            double sum = 0; int n = 0;
            for (int i = S.History.Count - 1; i >= 0 && n < 3; i--, n++) sum += S.History[i].Income;
            double avg = n > 0 ? sum / n : 0;
            return Math.Max(Gd.DayIncomeFloor, avg);
        }

        public double AvgNet(int days = 3)
        {
            double sum = 0; int n = 0;
            for (int i = S.History.Count - 1; i >= 0 && n < days; i--, n++) sum += S.History[i].Net;
            return n > 0 ? sum / n : 0;
        }

        /// <summary>Consumable coin pack: days × the usual day income. Returns the amount.</summary>
        public double GrantCash(double days)
        {
            double a = Math.Round(Math.Max(0, days) * DayIncome());
            S.Cash += a;
            Emit("cash_pack", -1, -1, a);
            return a;
        }

        /// <summary>Starter offer: one extra incubator slot (kept in the career; consumables are never restored).</summary>
        public void GrantIncubator() { if (S.Incubators < IncubatorCap) S.Incubators++; }

        // ------------------------------------------------------------------------------------------------ purchases

        static (string kind, string arg) Split(string id)
        {
            int c = id.IndexOf(':');
            return c < 0 ? (id, "") : (id.Substring(0, c), id.Substring(c + 1));
        }

        int PaddockArg(string arg) => int.TryParse(arg, out int p) && p >= 0 && p < S.Paddocks.Count ? p : -1;

        /// <summary>Price + whether it can be bought now. Ids: paddock:&lt;node&gt;, fence:&lt;p&gt;, water:&lt;p&gt;, shade:&lt;p&gt;,
        /// expand:&lt;p&gt;, zone:&lt;id&gt;, hire:&lt;role&gt;, tram_car, lane, vet_beds, cafe, gift, incubator, hatch_tech, chief,
        /// cooler:&lt;zone&gt;, feed_run2.</summary>
        public BuyInfo CanBuy(string id)
        {
            var r = new BuyInfo();
            if (string.IsNullOrEmpty(id)) { r.Reason = "invalid"; return r; }
            var (kind, arg) = Split(id);
            switch (kind)
            {
                case "paddock":
                {
                    int node = Gd.Node(arg);
                    if (node < 0 || Gd.Nodes[node].Kind != "paddock") { r.Reason = "invalid"; return r; }
                    if (PaddockAt(node) >= 0) { r.Reason = "owned"; return r; }
                    if (!Unlocked(Gd.Nodes[node].Zone)) { r.Reason = "locked"; return r; }
                    r.Cost = Gd.Price("paddock");
                    break;
                }
                case "fence":
                {
                    int p = PaddockArg(arg);
                    if (p < 0) { r.Reason = "invalid"; return r; }
                    if (S.Paddocks[p].Fence >= 3) { r.Reason = "max"; return r; }
                    r.Cost = Gd.Price(S.Paddocks[p].Fence == 1 ? "fence2" : "fence3");
                    break;
                }
                case "water":
                case "shade":
                {
                    int p = PaddockArg(arg);
                    if (p < 0) { r.Reason = "invalid"; return r; }
                    if (kind == "water" ? S.Paddocks[p].Water : S.Paddocks[p].Shade) { r.Reason = "owned"; return r; }
                    r.Cost = Gd.Price(kind);
                    break;
                }
                case "expand":
                {
                    int p = PaddockArg(arg);
                    if (p < 0) { r.Reason = "invalid"; return r; }
                    if (S.Paddocks[p].Expand >= Gd.ExpandMax) { r.Reason = "max"; return r; }
                    r.Cost = Gd.Price("expand");
                    break;
                }
                case "zone":
                {
                    var z = Gd.Zone(arg);
                    if (z == null) { r.Reason = "invalid"; return r; }
                    if (Unlocked(arg)) { r.Reason = "owned"; return r; }
                    if (z.Chapter > Chapter + 1) { r.Reason = "locked"; return r; }
                    if (S.Rating < z.Rating) { r.Reason = "rating"; return r; }
                    r.Cost = z.Cost;
                    break;
                }
                case "hire":
                    if (!Roles.Valid(arg)) { r.Reason = "invalid"; return r; }
                    if (CountRole(arg) >= RangerCap) { r.Reason = "max"; return r; }
                    r.Cost = Gd.Price("hire");
                    break;
                case "tram_car":
                    if (S.Tram.Cars >= Gd.TramCarsMax) { r.Reason = "max"; return r; }
                    r.Cost = Gd.Price("tram_car");
                    break;
                case "cafe":
                    if (S.Cafe >= 2) { r.Reason = "max"; return r; }
                    r.Cost = Gd.Price(S.Cafe == 0 ? "cafe1" : "cafe2");
                    break;
                case "incubator":
                    if (S.Incubators >= IncubatorCap) { r.Reason = "max"; return r; }
                    r.Cost = Gd.Price("incubator");
                    break;
                case "cooler":
                    if (!S.Stations.TryGetValue(arg, out var st)) { r.Reason = Gd.Zone(arg) == null ? "invalid" : "locked"; return r; }
                    if (st.Cooler) { r.Reason = "owned"; return r; }
                    r.Cost = Gd.Price("cooler");
                    break;
                case "chief":
                    if (ChiefAvailable) { r.Reason = "owned"; return r; }
                    if (S.Rating < Gd.ChiefRating) { r.Reason = "rating"; return r; }
                    r.Cost = Gd.Price("chief");
                    break;
                case "lane":
                case "vet_beds":
                case "gift":
                case "hatch_tech":
                case "feed_run2":
                    if (Has(kind)) { r.Reason = "owned"; return r; }
                    r.Cost = Gd.Price(kind);
                    break;
                default: r.Reason = "invalid"; return r;
            }
            if (S.Cash < r.Cost) { r.Reason = "cash"; return r; }
            r.Ok = true;
            return r;
        }

        /// <summary>Buys it. "" = ok, else the CanBuy reason.</summary>
        public string Buy(string id)
        {
            var info = CanBuy(id);
            if (!info.Ok) return info.Reason;
            S.Cash -= info.Cost;
            var (kind, arg) = Split(id);
            switch (kind)
            {
                case "paddock": S.Paddocks.Add(new PaddockState { Node = Gd.Node(arg) }); Emit("paddock_new", S.Paddocks.Count - 1); break;
                case "fence": S.Paddocks[PaddockArg(arg)].Fence++; S.Paddocks[PaddockArg(arg)].Integrity = 1; break;
                case "water": S.Paddocks[PaddockArg(arg)].Water = true; break;
                case "shade": S.Paddocks[PaddockArg(arg)].Shade = true; break;
                case "expand": S.Paddocks[PaddockArg(arg)].Expand++; break;
                case "zone": UnlockZone(arg, true); break;
                case "hire": AddRanger(arg, false); break;
                case "tram_car": S.Tram.Cars++; break;
                case "cafe": S.Cafe++; break;
                case "incubator": S.Incubators++; break;
                case "cooler": S.Stations[arg].Cooler = true; break;
                case "chief": S.Upgrades.Add("chief"); S.ChiefOn = true; break;
                default: S.Upgrades.Add(kind); break;
            }
            Emit("bought", -1, -1, info.Cost, id);
            return "";
        }

        public string SetChief(bool on)
        {
            if (!ChiefAvailable) return "locked";
            S.ChiefOn = on;
            return "";
        }

        // ------------------------------------------------------------------------------------------------ offline

        /// <summary>The time a save was written (unix seconds).</summary>
        public void Touch(long now) { S.LastSeen = now; }

        /// <summary>Back from away: eggs catch up; the sanctuary keeps earning by its plans (rate 50 %, cap 4 h; Night Shift
        /// 75 % / 10 h). Null when nothing was earned.</summary>
        public OfflineSummary Offline(long now)
        {
            if (S.LastSeen <= 0) { S.LastSeen = now; return null; }
            double away = now - S.LastSeen;
            S.LastSeen = now;
            if (away < Gd.OfflineMinS) return null;
            TickEggs(Math.Min(away, 6 * 3600));
            bool plus = Perk("offline_plus");
            double capH = plus ? Gd.OfflineCapHPlus : Gd.OfflineCapH;
            double hours = Math.Min(away / 3600.0, capH);
            double amount = Math.Round(Math.Max(0, AvgNet()) * hours * Gd.OfflineDaysPerHour * (plus ? Gd.OfflineRatePlus : Gd.OfflineRate));
            if (amount <= 0) return null;
            S.Cash += amount;
            S.OfflinePending = amount;
            return new OfflineSummary { Seconds = away, Hours = hours, Amount = amount, Capped = away / 3600.0 > capH };
        }

        // ------------------------------------------------------------------------------------------------ rewards (sim side)

        public static readonly string[] RewardPlacements = { "dawn_double", "borrow_ranger", "warm_incubator", "storm_shelter", "scout_tip" };
        public static bool RewardIsCash(string placement) => placement == "dawn_double";

        int OldestOpenPin()
        {
            PinState best = null;
            foreach (var p in S.Pins) if (p.Ranger < 0 && (best == null || p.Deadline < best.Deadline)) best = p;
            return best?.Id ?? -1;
        }

        bool FenceTrouble() => S.Paddocks.Exists(p => p.Integrity < 0.999) || S.Pins.Exists(p => p.Kind == PinKinds.Fence || p.Kind == PinKinds.Escape);

        int HiddenSite()
        {
            for (int i = 0; i < Gd.DigSites.Count; i++)
                if (Unlocked(Gd.DigSites[i].Zone) && Gd.DigSites[i].Species.Count > 2 && !DigRevealed(i, 2)) return i;
            return -1;
        }

        int ColdestEgg()
        {
            int best = -1;
            for (int i = 0; i < S.Eggs.Count; i++) if (best < 0 || S.Eggs[i].LeftS > S.Eggs[best].LeftS) best = i;
            return best;
        }

        /// <summary>target: borrow_ranger = pin id, warm_incubator = egg slot, scout_tip = dig site index (-1 = pick one).</summary>
        public RewardOfferInfo RewardOffer(string placement, long now, int target = -1)
        {
            var o = new RewardOfferInfo();
            if (!Gd.Rewards.TryGetValue(placement ?? "", out var def)) { o.Reason = "invalid"; return o; }
            if (S.Tutorial) { o.Reason = "tutorial"; return o; }
            long day = now / 86400;
            if (S.Rewards.TryGetValue(placement, out var u))
            {
                if (u.UtcDay == day && u.Count >= def.PerDay) { o.Reason = "cap"; return o; }
                if (def.CooldownS > 0 && now - u.LastAt < def.CooldownS) { o.Reason = "cooldown"; return o; }
            }
            double single = Math.Min(Gd.RewardSingleMaxDays, def.Days) * DayIncome();
            switch (placement)
            {
                case "dawn_double":
                    if (S.OfflinePending <= 0) { o.Reason = "context"; return o; }
                    o.Amount = Math.Round(Math.Min(S.OfflinePending, single)); break;
                case "borrow_ranger":
                {
                    if (S.Phase != Phases.Live) { o.Reason = "context"; return o; }
                    var p = target >= 0 ? Pin(target) : Pin(OldestOpenPin());
                    if (p == null || p.Ranger >= 0) { o.Reason = "context"; return o; }
                    break;
                }
                case "warm_incubator":
                    if (target >= S.Eggs.Count || (target < 0 && ColdestEgg() < 0)) { o.Reason = "context"; return o; }
                    break;
                case "storm_shelter":
                    if (!FenceTrouble()) { o.Reason = "context"; return o; }
                    break;
                case "scout_tip":
                    if (target >= 0 ? target >= Gd.DigSites.Count || DigRevealed(target, 2) : HiddenSite() < 0) { o.Reason = "context"; return o; }
                    break;
            }
            o.Ok = true;
            return o;
        }

        public RewardResult GrantReward(string placement, long now, int target = -1)
        {
            var r = new RewardResult();
            var o = RewardOffer(placement, now, target);
            if (!o.Ok) { r.Reason = o.Reason; return r; }
            switch (placement)
            {
                case "dawn_double": S.Cash += o.Amount; S.OfflinePending = 0; r.Amount = o.Amount; break;
                case "borrow_ranger":
                {
                    var p = target >= 0 ? Pin(target) : Pin(OldestOpenPin());
                    var b = AddRanger(RoleFor(p.Kind), true);
                    b.AtNode = p.Node;
                    b.Pin = p.Id;
                    b.Leg = 2;
                    b.Status = RangerStatus.Work;
                    b.TimeLeft = 2 + WorkMin(b, p) * 0.5;   // a ranger from the next ridge, already on site in ~2 minutes
                    p.Ranger = b.Id;
                    r.Amount = p.Id;
                    break;
                }
                case "warm_incubator":
                {
                    var e = S.Eggs[target >= 0 ? target : ColdestEgg()];
                    e.LeftS = Math.Max(1, e.LeftS - Gd.WarmS);
                    break;
                }
                case "storm_shelter":
                    foreach (var pd in S.Paddocks) pd.Integrity = 1;
                    foreach (var p in new List<PinState>(S.Pins))
                        if (p.Kind == PinKinds.Fence || p.Kind == PinKinds.Escape)
                        {
                            int ri = RangerIndex(p.Ranger);
                            Resolve(p);
                            if (ri >= 0) GoHome(S.Rangers[ri]);
                        }
                    break;
                case "scout_tip":
                {
                    int site = target >= 0 ? target : HiddenSite();
                    S.Scouted.Add(Gd.DigSites[site].Id);
                    r.Amount = site;
                    break;
                }
            }
            long day = now / 86400;
            if (!S.Rewards.TryGetValue(placement, out var u) || u.UtcDay != day) u = new RewardUse { UtcDay = day };
            u.Count++;
            u.LastAt = now;
            S.Rewards[placement] = u;
            r.Ok = true;
            Emit("reward", -1, -1, r.Amount, placement);
            return r;
        }

        /// <summary>The guide finished or was skipped: pins may fail, island events may start (after the free days).</summary>
        public void EndTutorial()
        {
            S.Tutorial = false;
            if (S.Phase == Phases.Live) S.GateOpen = true;
            // pins opened in the guide get a fresh deadline (no instant failure)
            foreach (var p in S.Pins) p.Deadline = Math.Max(p.Deadline, S.Minute + Gd.Pins[p.Kind].Deadline);
        }
    }
}
