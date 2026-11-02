// Docket Day sim core — the day cycle, the economy, purchases, research, districts, offline pay and the sim side of
// rewarded placements (caps + cooldowns persisted in the career; the kit's RewardedController enforces its own too).
using System;
using System.Collections.Generic;

namespace DocketDay.Sim
{
    public sealed class OfflineSummary { public double Seconds, Hours, Amount; public bool Capped; }
    public sealed class RewardOfferInfo { public bool Ok; public double Amount; public string Reason = ""; }
    public sealed class RewardResult { public bool Ok; public double Amount; public string Reason = ""; }
    public sealed class BuyInfo { public bool Ok; public double Cost; public string Reason = ""; }

    public sealed partial class Simulation
    {
        // ------------------------------------------------------------------------------------------------ day cycle

        /// <summary>Plan -> live (09:00 dispatch).</summary>
        public bool StartDay()
        {
            if (S.Phase != Phases.Plan) return false;
            S.Phase = Phases.Live;
            S.Paused = false;
            Emit("day_live", S.Day);
            return true;
        }

        void EndDay()
        {
            S.Minute = Gd.DayEnd;
            // every truck comes home (remaining km charged), loads go to the intake
            for (int ti = 0; ti < S.Trucks.Count; ti++)
            {
                var t = S.Trucks[ti];
                if (!t.Busy) continue;
                if (t.Status == TruckStatus.Drive || t.Status == TruckStatus.Broken)
                {
                    int from = t.Path.Count > 0 ? t.Path[t.Path.Count - 1] : t.AtNode;
                    double rest = Math.Max(0, t.PathKm - t.PathPos) + (t.Target >= 0 ? Gd.Graph.Km(from, Gd.DepotNode, VehicleOf(t).Alley) : 0);
                    if (rest < RoadGraph.Inf) ChargeKm(t, rest);
                }
                else if (t.Status == TruckStatus.Service && t.AtNode != Gd.DepotNode)
                {
                    if (t.Target >= 0 && t.Target < S.Points.Count) Pick(ti, t.Target);
                    double back = Gd.Graph.Km(t.AtNode, Gd.DepotNode, VehicleOf(t).Alley);
                    if (back < RoadGraph.Inf) ChargeKm(t, back);
                }
                for (int m = 0; m < Mats.N; m++) { S.Depot.Intake[m] += t.Load[m]; t.Load[m] = 0; }
                if (t.Status != TruckStatus.Idle) t.Trips++;
                t.Status = TruckStatus.Idle; t.AtNode = Gd.DepotNode; t.Target = -1; t.Stops.Clear(); t.Path.Clear(); t.PathKm = t.PathPos = 0;
                t.Voucher = false; t.ResumeStatus = "";
            }
            // the depot keeps counting / baling for the wrap-up (second shift research: one more hour)
            int wrap = Gd.DepotWrapupMin + (Has("second_shift") ? 60 : 0);
            for (int k = 0; k < wrap; k++) StepDepot(1.0);
            // technicians finish what lands before they clock off; open calls stay for tomorrow
            foreach (var j in S.TechJobs) if (j.Kind != "done" && j.DoneAt <= Gd.DayEnd + wrap) FinishTech(j);
            S.TechJobs.Clear();
            foreach (var p in S.Points) p.TechCalled = false;
            CollectTill();

            var td = S.Today;
            td.Wages = S.Drivers * Gd.DriverWage + S.Techs * Gd.TechWage;
            foreach (var p in S.Points) td.Leases += KindOf(p).LeaseDay;
            td.Power = Gd.DepotPower + Math.Max(0, S.Depot.Lines - 1) * Gd.LinePower;
            S.Cash -= td.Wages + td.Leases + td.Power;
            td.BacklogEnd = S.Depot.IntakeTotal;
            td.CountCap = CountPerMinute * (Gd.DayMinutes + wrap);
            if (!S.Tutorial && S.Day > Gd.EventFreeDays && td.BacklogEnd > td.CountCap * 0.8)
            {
                // the audit: an overloaded count line produces count mismatches
                double fine = Math.Round(Math.Max(100, DayIncome() * 0.1));
                td.Fines += fine;
                S.Cash -= fine;
                Emit("crisis", -1, -1, fine, "audit");
            }
            td.StarService = StarsBelow(td.Arrivals <= 0 ? 0 : td.TurnedRate, Gd.StarService);
            td.StarEfficiency = td.Collected <= 0 ? 0 : StarsAbove(td.PerKm, Gd.StarEfficiency);
            td.StarThroughput = StarsBelow(td.CountCap <= 0 ? 0 : td.BacklogEnd / td.CountCap, Gd.StarThroughput);
            double score = td.Stars / 9.0 * 100.0;
            double next = S.Rating + Gd.RatingAlpha * (score - S.Rating);
            // forgiving start: the guide and the first days can only raise the rating
            if (S.Tutorial || S.Day <= Gd.TurnedRatingFreeDays) next = Math.Max(S.Rating, next);
            S.Rating = Math.Max(0, Math.Min(100, next));
            if (td.Stars > S.BestDayStars) S.BestDayStars = td.Stars;
            if (td.Stars >= 8) S.ThreeStarDays++;
            S.History.Add(td);
            while (S.History.Count > 14) S.History.RemoveAt(0);
            S.Phase = Phases.Report;
            Emit("day_done", S.Day, td.Stars, td.Net);
        }

        static int StarsBelow(double x, double[] th)
        {
            // th = [1 star, 2 stars, 3 stars] upper bounds, decreasing
            int s = 0;
            for (int i = 0; i < th.Length; i++) if (x <= th[i] + 1e-12) s = i + 1;
            return s;
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
            foreach (var p in S.Points) { p.FullMin = 0; p.Arrivals = p.Turned = p.Deposited = p.Picked = 0; p.BusyUntil = 0; }
            foreach (var t in S.Trucks) { t.KmToday = 0; t.Trips = 0; }
            S.Events.RemoveAll(e => e.UntilDay < S.Day);
            UpdateMarket();
            PlanDay();
            S.NextDispatchAt = Gd.DayStart;
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

        /// <summary>Consumable cash pack: days × the usual day income. Returns the amount.</summary>
        public double GrantCash(double days)
        {
            double a = Math.Round(Math.Max(0, days) * DayIncome());
            S.Cash += a;
            Emit("cash_pack", -1, -1, a);
            return a;
        }

        // ------------------------------------------------------------------------------------------------ purchases

        /// <summary>Price + whether it can be bought now. Ids: machine:&lt;node&gt;, shop:&lt;node&gt;, horeca:&lt;node&gt;,
        /// bin_m:&lt;point&gt;, compactor:&lt;point&gt;, sensor:&lt;point&gt;, truck:&lt;type&gt;, driver, tech, line, yard, intake,
        /// centre, dispatcher, district:&lt;id&gt;.</summary>
        public BuyInfo CanBuy(string id)
        {
            var r = new BuyInfo();
            if (string.IsNullOrEmpty(id)) { r.Reason = "invalid"; return r; }
            string kind = id, arg = "";
            int c = id.IndexOf(':');
            if (c >= 0) { kind = id.Substring(0, c); arg = id.Substring(c + 1); }
            var d = S.Depot;
            switch (kind)
            {
                case "machine":
                case "shop":
                case "horeca":
                {
                    int node = Gd.Node(arg);
                    if (node < 0 || Gd.Nodes[node].Kind != "site" || Gd.Nodes[node].Site != kind) { r.Reason = "invalid"; return r; }
                    if (PointAt(node) >= 0) { r.Reason = "owned"; return r; }
                    if (!Unlocked(Gd.Nodes[node].District)) { r.Reason = "locked"; return r; }
                    if (kind == "shop" && !Has("shop_points")) { r.Reason = "research"; return r; }
                    if (kind == "horeca" && !Has("horeca")) { r.Reason = "research"; return r; }
                    r.Cost = Gd.Price(kind == "machine" ? "machine_s" : kind);
                    break;
                }
                case "bin_m":
                case "compactor":
                case "sensor":
                {
                    if (!int.TryParse(arg, out int pi) || pi < 0 || pi >= S.Points.Count) { r.Reason = "invalid"; return r; }
                    var p = S.Points[pi];
                    if (!p.IsMachine) { r.Reason = "invalid"; return r; }
                    if (kind == "bin_m" && p.Kind == "machine_m") { r.Reason = "owned"; return r; }
                    if (kind == "compactor" && p.Compactor) { r.Reason = "owned"; return r; }
                    if (kind == "sensor" && p.Sensor) { r.Reason = "owned"; return r; }
                    if (kind == "compactor" && !Has("compactor")) { r.Reason = "research"; return r; }
                    if (kind == "sensor" && !Has("sensors")) { r.Reason = "research"; return r; }
                    r.Cost = Gd.Price(kind);
                    break;
                }
                case "truck":
                {
                    if (!Gd.Vehicles.TryGetValue(arg, out var v)) { r.Reason = "invalid"; return r; }
                    if (v.Research != "" && !Has(v.Research)) { r.Reason = "research"; return r; }
                    if (S.Trucks.Count >= 8) { r.Reason = "max"; return r; }
                    r.Cost = v.Price;
                    break;
                }
                case "driver":
                    if (S.Drivers >= S.Trucks.Count) { r.Reason = "max"; return r; }
                    r.Cost = Gd.Price("driver"); break;
                case "tech":
                    if (S.Techs >= 4) { r.Reason = "max"; return r; }
                    r.Cost = Gd.Price("tech"); break;
                case "line":
                    if (d.Lines >= Gd.LinesMax) { r.Reason = "max"; return r; }
                    r.Cost = Gd.Price("line"); break;
                case "yard":
                    if (d.YardCap >= Gd.YardCap * (Has("yard_plus") ? 4 : 2)) { r.Reason = Has("yard_plus") ? "max" : "research"; return r; }
                    r.Cost = Gd.Price("yard"); break;
                case "intake":
                    if (d.IntakeCap >= Gd.IntakeCap * (Has("intake_plus") ? 4 : 2)) { r.Reason = Has("intake_plus") ? "max" : "research"; return r; }
                    r.Cost = Gd.Price("intake"); break;
                case "centre":
                    if (d.Centre) { r.Reason = "owned"; return r; }
                    if (!Has("counting_centre")) { r.Reason = "research"; return r; }
                    r.Cost = Gd.Price("centre"); break;
                case "dispatcher":
                    if (d.Dispatcher) { r.Reason = "owned"; return r; }
                    if (!Has("dispatcher")) { r.Reason = "research"; return r; }
                    r.Cost = Gd.Price("dispatcher"); break;
                case "district":
                {
                    var dd = Gd.District(arg);
                    if (dd == null) { r.Reason = "invalid"; return r; }
                    if (Unlocked(arg)) { r.Reason = "owned"; return r; }
                    if (dd.Chapter > Chapter + 1) { r.Reason = "locked"; return r; }
                    if (S.Rating < dd.Rating) { r.Reason = "rating"; return r; }
                    r.Cost = dd.Cost;
                    break;
                }
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
            string kind = id, arg = "";
            int c = id.IndexOf(':');
            if (c >= 0) { kind = id.Substring(0, c); arg = id.Substring(c + 1); }
            var d = S.Depot;
            switch (kind)
            {
                case "machine": case "shop": case "horeca":
                {
                    int node = Gd.Node(arg);
                    var p = new PointState { Node = node, Kind = kind == "machine" ? "machine_s" : kind, LastServiceDay = S.Day, LastServiceMin = S.Minute };
                    if (kind != "machine") { p.Policy = Policies.Fixed; p.FixedEvery = 1; }
                    S.Points.Add(p);
                    Emit("point_new", S.Points.Count - 1);
                    break;
                }
                case "bin_m": S.Points[int.Parse(arg)].Kind = "machine_m"; ResetLitreCache(); break;
                case "compactor": S.Points[int.Parse(arg)].Compactor = true; ResetLitreCache(); break;
                case "sensor": S.Points[int.Parse(arg)].Sensor = true; break;
                case "truck":
                {
                    int id2 = 0;
                    foreach (var t in S.Trucks) id2 = Math.Max(id2, t.Id + 1);
                    S.Trucks.Add(new TruckState { Id = id2, Type = arg, AtNode = Gd.DepotNode });
                    break;
                }
                case "driver": S.Drivers++; break;
                case "tech": S.Techs++; break;
                case "line": d.Lines++; break;
                case "yard": d.YardCap += Gd.YardCap / 2; break;
                case "intake": d.IntakeCap += Gd.IntakeCap / 2; break;
                case "centre": d.Centre = true; break;
                case "dispatcher":
                    d.Dispatcher = true;
                    for (int i = 0; i < S.Trucks.Count && AutoCount < AutoSlots; i++) S.Trucks[i].Auto = true;
                    break;
                case "district": S.Districts.Add(arg); Emit("district", -1, -1, 0, arg); break;
            }
            Emit("bought", -1, -1, info.Cost, id);
            return "";
        }

        public string SetPolicy(int point, string policy, int value)
        {
            if (point < 0 || point >= S.Points.Count || !Policies.Valid(policy)) return "invalid";
            var p = S.Points[point];
            p.Policy = policy;
            if (policy == Policies.Threshold) p.Threshold = Math.Max(30, Math.Min(100, value <= 0 ? 80 : value));
            if (policy == Policies.Fixed) p.FixedEvery = Math.Max(1, Math.Min(7, value <= 0 ? 1 : value));
            Emit("policy", point, -1, value, policy);
            return "";
        }

        // ------------------------------------------------------------------------------------------------ research

        public string CanResearch(string id)
        {
            if (!Gd.ResearchById.TryGetValue(id ?? "", out var r)) return "invalid";
            if (Has(id)) return "owned";
            if (S.Researching != "") return S.Researching == id ? "running" : "busy";
            foreach (var q in r.Requires) if (!Has(q)) return "requires";
            if (!string.IsNullOrEmpty(r.District) && !Unlocked(r.District)) return "locked";
            if (S.Cash < r.Cost) return "cash";
            return "";
        }

        public string StartResearch(string id)
        {
            string why = CanResearch(id);
            if (why != "") return why;
            var r = Gd.ResearchById[id];
            S.Cash -= r.Cost;
            S.Researching = id;
            S.ResearchLeftS = r.Minutes * 60.0;
            Emit("research_start", -1, -1, r.Cost, id);
            return "";
        }

        /// <summary>Real seconds pass for the research timer only (balance bot / tests that advance game minutes directly).</summary>
        public void AdvanceResearch(double realS) { if (realS > 0) TickResearch(realS); }

        void TickResearch(double realS)
        {
            if (S.Researching == "") return;
            S.ResearchLeftS -= realS;
            if (S.ResearchLeftS > 0) return;
            string id = S.Researching;
            S.Researching = "";
            S.ResearchLeftS = 0;
            if (!S.Research.Contains(id)) S.Research.Add(id);
            ResetLitreCache();
            Emit("research_done", -1, -1, 0, id);
        }

        // ------------------------------------------------------------------------------------------------ offline

        /// <summary>The time a save was written (unix seconds).</summary>
        public void Touch(long now) { S.LastSeen = now; }

        /// <summary>Back from away: research catches up; the network keeps earning by its service policies (rate 50 %,
        /// cap 4 h; Offline Plus 75 % / 10 h). Null when nothing was earned.</summary>
        public OfflineSummary Offline(long now)
        {
            if (S.LastSeen <= 0) { S.LastSeen = now; return null; }
            double away = now - S.LastSeen;
            S.LastSeen = now;
            if (away < Gd.OfflineMinS) return null;
            TickResearch(away);
            bool plus = Perk("offline_plus");
            double capH = plus ? Gd.OfflineCapHPlus : Gd.OfflineCapH;
            double hours = Math.Min(away / 3600.0, capH);
            int auto = 0;
            foreach (var p in S.Points) if (p.Policy != Policies.Call) auto++;
            double share = S.Points.Count == 0 ? 0 : Math.Max(0.25, (double)auto / S.Points.Count);
            double amount = Math.Round(Math.Max(0, AvgNet()) * hours * Gd.OfflineDaysPerHour * (plus ? Gd.OfflineRatePlus : Gd.OfflineRate) * share);
            if (amount <= 0) return null;
            S.Cash += amount;
            S.OfflinePending = amount;
            return new OfflineSummary { Seconds = away, Hours = hours, Amount = amount, Capped = away / 3600.0 > capH };
        }

        // ------------------------------------------------------------------------------------------------ rewards (sim side)

        public static readonly string[] RewardPlacements = { "offline_x2", "emergency_empty", "fuel_voucher", "baler_rescue", "daily_bonus" };
        public static bool RewardIsCash(string placement) => placement == "offline_x2" || placement == "daily_bonus";

        int FullestFullPoint()
        {
            int best = -1; double bp = 0;
            for (int i = 0; i < S.Points.Count; i++)
            {
                double f = FillPct(S.Points[i]);
                if (f >= 0.95 && f > bp) { bp = f; best = i; }
            }
            return best;
        }

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
            double single = Gd.RewardSingleMaxDays * DayIncome();
            switch (placement)
            {
                case "offline_x2":
                    if (S.OfflinePending <= 0) { o.Reason = "context"; return o; }
                    o.Amount = Math.Round(Math.Min(S.OfflinePending, single)); break;
                case "emergency_empty":
                    if (S.Phase == Phases.Report) { o.Reason = "context"; return o; }
                    if (target >= 0 ? (target >= S.Points.Count || FillPct(S.Points[target]) < 0.95) : FullestFullPoint() < 0) { o.Reason = "context"; return o; }
                    break;
                case "fuel_voucher":
                    if (S.FuelVoucher || S.Phase == Phases.Report) { o.Reason = "context"; return o; }
                    break;
                case "baler_rescue":
                    if (!S.Depot.BalerDown) { o.Reason = "context"; return o; }
                    break;
                case "daily_bonus":
                    o.Amount = Math.Round(Math.Min(def.Days * DayIncome(), single)); break;
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
                case "offline_x2": S.Cash += o.Amount; S.OfflinePending = 0; r.Amount = o.Amount; break;
                case "daily_bonus": S.Cash += o.Amount; r.Amount = o.Amount; break;
                case "fuel_voucher": S.FuelVoucher = true; break;
                case "baler_rescue":
                    S.Depot.BalerDown = false;
                    S.TechJobs.RemoveAll(j => j.Kind == "baler");
                    break;
                case "emergency_empty":
                {
                    int i = target >= 0 ? target : FullestFullPoint();
                    var p = S.Points[i];
                    double n = 0;
                    for (int m = 0; m < Mats.N; m++) { S.Depot.Intake[m] += p.Fill[m]; n += p.Fill[m]; p.Fill[m] = 0; }
                    p.EstVol = 0; p.LastServiceDay = S.Day; p.LastServiceMin = S.Minute;
                    S.Today.Collected += (int)n; S.Containers += (long)n;
                    r.Amount = n;
                    Emit("serviced", -1, i, n, "emergency");
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

        /// <summary>The guide finished or was skipped: crises and penalties may start (after the free days).</summary>
        public void EndTutorial() { S.Tutorial = false; }
    }
}
