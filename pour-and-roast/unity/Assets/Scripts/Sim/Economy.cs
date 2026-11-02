// Pour & Roast sim core — the day cycle, the economy, purchases, research, branches, offline pay and the sim side of the
// rewarded placements (caps + cooldowns persisted in the career; the kit's RewardedController enforces its own too).
using System;
using System.Collections.Generic;

namespace PourAndRoast.Sim
{
    public sealed class OfflineSummary { public double Seconds, Hours, Amount; public bool Capped; }
    public sealed class RewardOfferInfo { public bool Ok; public double Amount; public string Reason = ""; }
    public sealed class RewardResult { public bool Ok; public double Amount; public string Reason = ""; }
    public sealed class BuyInfo { public bool Ok; public double Cost; public string Reason = ""; }

    public sealed partial class Simulation
    {
        // ------------------------------------------------------------------------------------------------ day cycle

        /// <summary>Plan -> live (07:00, the shutters go up).</summary>
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
            var td = S.Today;
            // closing time: finished cups go out, the rest of the rail is closed (no review, nothing stuck overnight)
            for (int i = S.Tickets.Count - 1; i >= 0; i--)
            {
                var t = S.Tickets[i];
                if (t.Ready && !AllFailed(t)) Handover(i, false);
            }
            S.Tickets.Clear();
            S.HelperUntil = -1;
            S.AutoTuneUntil = -1;
            // waste: half the milk spoils, pastries and the pre-brew urn do not keep
            double spoilValue = 0;
            for (int k = 0; k < 2; k++)
            {
                double lost = S.Milk[k] * Gd.MilkSpoil;
                spoilValue += lost * MilkPrice(k);
                S.Milk[k] = Math.Round(S.Milk[k] - lost, 3);
            }
            for (int i = 0; i < S.Pastry.Length; i++)
            {
                if (S.Pastry[i] <= 0) continue;
                spoilValue += S.Pastry[i] * Gd.Menu[i].Price * PastryCostShare;
                S.Pastry[i] = 0;
            }
            S.PrepHot = 0;
            td.Waste = Math.Round(spoilValue, 2);
            // wages + rent of the venue you ran today (the other branches pay their own out of their income)
            foreach (var s in S.Staff)
                if (s.Venue == S.Venue || s.Role == Roles.Roaster) td.Wages += Gd.Roles.TryGetValue(s.Role, out var r) ? r.Wage : 0;
            td.Rent = VenueDef.Rent;
            S.Cash -= td.Wages + td.Rent;
            // branches run by their managers
            foreach (var v in S.Venues)
            {
                if (v.Id == S.Venue) continue;
                double b = Math.Round(BranchIncome(v));
                td.Branches += b;
                S.Cash += b;
            }
            DeliverContracts();
            // reviews -> reputation (forgiving start: the guide and the first days can only raise it)
            if (td.ReviewN > 0)
            {
                double next = S.Rating + Gd.RatingAlpha * (td.AvgReview - S.Rating);
                if (S.Tutorial || S.Day <= Gd.RatingFreeDays) next = Math.Max(S.Rating, next);
                S.Rating = Math.Round(Math.Max(0, Math.Min(5, next)), 3);
            }
            var av = Venue;
            if (av != null)
            {
                av.DaysRun++;
                double own = td.Net - td.Branches - td.Wholesale;
                if (own > av.BestNet) av.BestNet = Math.Round(own, 2);
            }
            if (td.Stars > S.BestDayStars) S.BestDayStars = td.Stars;
            if (td.Stars >= 5) S.FiveStarDays++;
            S.History.Add(td);
            while (S.History.Count > 14) S.History.RemoveAt(0);
            S.Phase = Phases.Report;
            Emit("day_done", S.Day, td.Stars, td.Net);
        }

        public double BranchIncome(VenueState v)
        {
            var d = Gd.Venue(v.Id);
            if (d == null) return 0;
            return Math.Max(d.AutoFloor, v.BestNet * Gd.BranchShare);
        }

        double MilkPrice(int k) => (Gd.Suppliers.TryGetValue(S.Supplier, out var s) ? s.Price : 1.1) * (k == 1 ? Gd.OatPriceMult : 1.0);
        double PastryCostShare => Has("pastry_oven") ? Gd.OvenCostShare : Gd.PastryCostShare;

        /// <summary>Report -> next morning's plan: the forecast moves on, the supplier delivers, the case is baked to par.</summary>
        public bool NextDay()
        {
            if (S.Phase != Phases.Report) return false;
            S.Day++;
            S.Minute = Gd.DayStart;
            if (S.Forecast.Count > 0) S.Forecast.RemoveAt(0);
            while (S.Forecast.Count < 4) S.Forecast.Add(ForecastFor(S.Day + S.Forecast.Count));
            S.Today = new DayStats { Day = S.Day, Venue = S.Venue, Weather = Weather };
            S.Events.RemoveAll(e => e.UntilDay < S.Day);
            UpdateMarket();
            Restock();
            PlanEvents();
            if (HasRoaster) Blend();
            S.Phase = Phases.Plan;
            S.Paused = false;
            Emit("day_start", S.Day);
            return true;
        }

        void Restock()
        {
            var td = S.Today;
            // pastries baked to the par levels the player set (only what this venue serves)
            for (int i = 0; i < Gd.Menu.Count; i++)
            {
                var m = Gd.Menu[i];
                if (!m.IsPastry || !OnMenu(m)) continue;
                int need = Math.Max(0, S.Par[i] - S.Pastry[i]);
                double cost = need * m.Price * PastryCostShare;
                if (need <= 0 || S.Cash < cost) continue;
                S.Pastry[i] += need;
                S.Cash -= cost;
                td.PastryCost += Math.Round(cost, 2);
            }
            // the standing milk order (a cheap supplier is late more often)
            double milkCost = S.MilkOrder[0] * MilkPrice(0) + S.MilkOrder[1] * MilkPrice(1);
            S.Cash -= milkCost;
            td.MilkCost += Math.Round(milkCost, 2);
            S.MilkPending[0] = S.MilkOrder[0];
            S.MilkPending[1] = S.MilkOrder[1];
            S.MilkArriveAt = Gd.DayStart;
            if (CrisesAllowed && Gd.Suppliers.TryGetValue(S.Supplier, out var sup) && Rng.Frand() < sup.Late)
            {
                S.MilkArriveAt = Gd.DayStart + Gd.MilkLateMin;
                Emit("crisis", -1, -1, Gd.MilkLateMin, "milk_late");
            }
            // supplier beans topped up to the par (before the roastery, or as a fallback when the house blend runs out)
            if (S.AutoBeans)
            {
                int guard = 0;
                while (S.BeanKg < Gd.BeanParKg && guard++ < 6)
                    if (BuySupplierBeans() != "") break;
            }
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

        static (string kind, string arg) Split(string id)
        {
            int c = id.IndexOf(':');
            return c >= 0 ? (id.Substring(0, c), id.Substring(c + 1)) : (id, "");
        }

        /// <summary>Price + whether it can be bought now. Ids: staff:&lt;role&gt;, train:&lt;staff id&gt;, heads, wands, grinder,
        /// ice, rail, seats, decor, drum, venue:&lt;id&gt;, service_grinder, milk_run, ice_bag, beans, green:&lt;origin&gt;.</summary>
        public BuyInfo CanBuy(string id)
        {
            var r = new BuyInfo();
            if (string.IsNullOrEmpty(id)) { r.Reason = "invalid"; return r; }
            var (kind, arg) = Split(id);
            var v = Venue;
            var vd = VenueDef;
            switch (kind)
            {
                case "staff":
                {
                    if (!Gd.Roles.TryGetValue(arg, out var role)) { r.Reason = "invalid"; return r; }
                    if (role.Research != "" && !Has(role.Research)) { r.Reason = "research"; return r; }
                    if (arg == Roles.Roaster && !RoasteryOpen) { r.Reason = "locked"; return r; }
                    int have = arg == Roles.Roaster ? CountRole(arg) : CountRole(arg, S.Venue);
                    int max = arg == Roles.Barista ? Math.Min(role.Max, v.Heads + v.Wands + 1) : role.Max;
                    if (have >= max) { r.Reason = "max"; return r; }
                    r.Cost = role.Price;
                    break;
                }
                case "train":
                {
                    if (!int.TryParse(arg, out int sid)) { r.Reason = "invalid"; return r; }
                    var s = StaffById(sid);
                    if (s == null) { r.Reason = "invalid"; return r; }
                    if (s.Level >= Gd.LevelMax || s.Level >= Gd.TrainPrice.Length) { r.Reason = "max"; return r; }
                    r.Cost = Gd.TrainPrice[s.Level];
                    break;
                }
                case "heads":
                    if (v.Heads >= vd.HeadsMax || v.Heads + 1 > Gd.MaxLevel("heads")) { r.Reason = "max"; return r; }
                    r.Cost = Gd.Price("heads", v.Heads + 1); break;
                case "wands":
                    if (v.Wands >= vd.WandsMax || v.Wands + 1 > Gd.MaxLevel("wands")) { r.Reason = "max"; return r; }
                    r.Cost = Gd.Price("wands", v.Wands + 1); break;
                case "grinder":
                    if (v.Grinder >= Gd.MaxLevel("grinder")) { r.Reason = "max"; return r; }
                    r.Cost = Gd.Price("grinder", v.Grinder + 1); break;
                case "ice":
                    if (Tier < 2) { r.Reason = "locked"; return r; }
                    if (v.Ice >= Gd.MaxLevel("ice")) { r.Reason = "max"; return r; }
                    r.Cost = Gd.Price("ice", v.Ice + 1); break;
                case "rail":
                    if (v.Rail >= Gd.MaxLevel("rail")) { r.Reason = "max"; return r; }
                    r.Cost = Gd.Price("rail", v.Rail + 1); break;
                case "seats":
                    if (v.Seats >= vd.SeatsMax || v.Seats >= Gd.MaxLevel("seats")) { r.Reason = "max"; return r; }
                    r.Cost = Gd.Price("seats", v.Seats + 1); break;
                case "decor":
                    if (v.Decor >= Gd.MaxLevel("decor")) { r.Reason = "max"; return r; }
                    r.Cost = Gd.Price("decor", v.Decor + 1); break;
                case "drum":
                    if (!RoasteryOpen) { r.Reason = "locked"; return r; }
                    if (S.Drum >= Gd.MaxLevel("drum") || S.Drum + 1 >= Gd.DrumKg.Length) { r.Reason = "max"; return r; }
                    r.Cost = Gd.Price("drum", S.Drum + 1); break;
                case "venue":
                {
                    var d = Gd.Venue(arg);
                    if (d == null) { r.Reason = "invalid"; return r; }
                    if (Owns(arg)) { r.Reason = "owned"; return r; }
                    if (d.Chapter > Chapter + 1) { r.Reason = "locked"; return r; }
                    if (S.Rating < d.Rating) { r.Reason = "rating"; return r; }
                    r.Cost = d.Cost;
                    break;
                }
                case "service_grinder":
                    if (v.Wear < 1) { r.Reason = "max"; return r; }
                    r.Cost = Gd.GrinderServicePrice; break;
                case "milk_run":
                    if (S.RunArriveAt >= 0) { r.Reason = "busy"; return r; }
                    r.Cost = Math.Round((Gd.RunL[0] * MilkPrice(0) + Gd.RunL[1] * MilkPrice(1)) * Gd.RunMult * (HasRunner ? 0.75 : 1.0)); break;
                case "ice_bag":
                    if (Tier < 2) { r.Reason = "locked"; return r; }
                    r.Cost = Gd.IceBagPrice; break;
                case "beans":
                    r.Cost = Math.Round(Gd.SupplierPrice * Gd.SupplierLot); break;
                case "green":
                {
                    int oi = Gd.OriginIndex(arg);
                    if (oi < 0) { r.Reason = "invalid"; return r; }
                    if (!RoasteryOpen) { r.Reason = "locked"; return r; }
                    if (!OriginOpen(Gd.Origins[oi])) { r.Reason = "research"; return r; }
                    r.Cost = LotCost(oi);
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
            var (kind, arg) = Split(id);
            if (kind == "green") return BuyGreen(arg);
            if (kind == "beans") return BuySupplierBeans();
            S.Cash -= info.Cost;
            var v = Venue;
            switch (kind)
            {
                case "staff":
                {
                    string station = Stations.Any;
                    if (arg == Roles.Barista)
                    {
                        // a new barista takes the station nobody covers yet (the steam wand first)
                        bool milk = false, esp = false; int baristas = 0;
                        foreach (var s in StaffHere()) if (s.Role == Roles.Barista) { baristas++; if (s.Station == Stations.Milk) milk = true; if (s.Station == Stations.Espresso) esp = true; }
                        if (baristas >= 1 && !milk) station = Stations.Milk;
                        else if (baristas >= 1 && !esp) station = Stations.Espresso;
                        if (baristas == 1 && station == Stations.Milk)
                            foreach (var s in StaffHere()) if (s.Role == Roles.Barista && s.Station == Stations.Any) s.Station = Stations.Espresso;
                    }
                    var ns = Hire(arg, station);
                    if (arg == Roles.Roaster) ns.Venue = "roastery";
                    Emit("hired", ns.Id, -1, info.Cost, arg);
                    break;
                }
                case "train": StaffById(int.Parse(arg)).Level++; break;
                case "heads": v.Heads++; break;
                case "wands": v.Wands++; break;
                case "grinder": v.Grinder++; v.Wear = 0; break;
                case "ice": v.Ice++; break;
                case "rail": v.Rail++; break;
                case "seats": v.Seats++; break;
                case "decor": v.Decor++; break;
                case "drum": S.Drum++; break;
                case "service_grinder": v.Wear = 0; break;
                case "milk_run":
                    S.RunPending[0] += Gd.RunL[0]; S.RunPending[1] += Gd.RunL[1];
                    S.RunArriveAt = S.Phase == Phases.Live ? S.Minute + (HasRunner ? Gd.RunMinRunner : Gd.RunMin) : S.Minute;
                    S.Today.MilkCost += info.Cost;
                    if (S.Phase != Phases.Live) { StepSupplies(0); }
                    break;
                case "ice_bag": S.Ice += Gd.IceBagKg; S.Today.IceCost += info.Cost; break;
                case "venue": UnlockVenue(arg); break;
            }
            Emit("bought", -1, -1, info.Cost, id);
            return "";
        }

        void UnlockVenue(string id)
        {
            var d = Gd.Venue(id);
            S.Venues.Add(NewVenue(d));
            // the lease comes with a barista; you move in (the old venue keeps running as a branch)
            string old = S.Venue;
            if (S.Phase == Phases.Plan) S.Venue = id;
            var b = Hire(Roles.Barista, Stations.Any);
            b.Venue = id;
            foreach (var m in Gd.Menu) if (m.Tier == d.Chapter && !S.Menu.Contains(m.Id)) S.Menu.Add(m.Id);
            Emit("venue", -1, -1, d.Cost, id);
            if (old != S.Venue) S.Today.Venue = S.Venue;
        }

        /// <summary>Works another owned branch (morning plan only).</summary>
        public string SwitchVenue(string id)
        {
            if (!Owns(id)) return "invalid";
            if (S.Phase != Phases.Plan) return "phase";
            if (S.Venue == id) return "";
            S.Venue = id;
            S.Today.Venue = id;
            Emit("venue_switch", -1, -1, 0, id);
            return "";
        }

        public string ToggleMenu(string itemId, bool on)
        {
            var m = Gd.Item(itemId);
            if (m == null || !Available(m)) return "invalid";
            if (on) { if (!S.Menu.Contains(itemId)) S.Menu.Add(itemId); return ""; }
            if (!m.IsPastry && MenuCount <= 1 && OnMenu(m)) return "last";
            S.Menu.Remove(itemId);
            return "";
        }

        public string SetPar(string itemId, int n)
        {
            int i = Gd.MenuIndex(itemId);
            if (i < 0 || !Gd.Menu[i].IsPastry) return "invalid";
            S.Par[i] = Math.Max(0, Math.Min(Gd.PastryParMax, n));
            return "";
        }

        public string SetMilkOrder(int kind, double litres)
        {
            if (kind < 0 || kind > 1) return "invalid";
            S.MilkOrder[kind] = Math.Max(0, Math.Min(80, Math.Round(litres)));
            return "";
        }

        public string SetSupplier(string id)
        {
            if (!Gd.Suppliers.ContainsKey(id ?? "")) return "invalid";
            S.Supplier = id;
            return "";
        }

        /// <summary>Forecast prep: hot = a pre-brew urn (the next simple hot coffees are served at once), ice = extra ice.</summary>
        public string Prep(string kind)
        {
            if (S.Phase == Phases.Report) return "phase";
            if (kind == "hot")
            {
                if (S.Cash < Gd.PrepHotPrice) return "cash";
                S.Cash -= Gd.PrepHotPrice;
                S.Today.BeansCost += Gd.PrepHotPrice;
                S.PrepHot += Gd.PrepHotCups;
                Emit("prep", 0, -1, Gd.PrepHotCups);
                return "";
            }
            if (kind == "ice")
            {
                if (Tier < 2) return "locked";
                if (S.Cash < Gd.PrepIcePrice) return "cash";
                S.Cash -= Gd.PrepIcePrice;
                S.Today.IceCost += Gd.PrepIcePrice;
                S.Ice += Gd.PrepIceKg;
                Emit("prep", 1, -1, Gd.PrepIceKg);
                return "";
            }
            return "invalid";
        }

        // ------------------------------------------------------------------------------------------------ research

        public string CanResearch(string id)
        {
            if (!Gd.ResearchById.TryGetValue(id ?? "", out var r)) return "invalid";
            if (Has(id)) return "owned";
            if (S.Researching != "") return S.Researching == id ? "running" : "busy";
            foreach (var q in r.Requires) if (!Has(q)) return "requires";
            if (!string.IsNullOrEmpty(r.Venue) && !Owns(r.Venue)) return "locked";
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

        /// <summary>Real seconds pass for the research timer and the roaster (balance bot / tests that advance game minutes directly).</summary>
        public void AdvanceResearch(double realS) { if (realS > 0) { TickResearch(realS); TickRoast(realS); } }

        void TickResearch(double realS)
        {
            if (S.Researching == "") return;
            S.ResearchLeftS -= realS;
            if (S.ResearchLeftS > 0) return;
            string id = S.Researching;
            S.Researching = "";
            S.ResearchLeftS = 0;
            if (!S.Research.Contains(id)) S.Research.Add(id);
            Emit("research_done", -1, -1, 0, id);
        }

        // ------------------------------------------------------------------------------------------------ offline

        /// <summary>The time a save was written (unix seconds).</summary>
        public void Touch(long now) { S.LastSeen = now; }

        /// <summary>Share of the usual profit the shop earns while you are away: the better it runs itself (auto beans, a
        /// cashier, a roaster assistant), the more.</summary>
        public double OfflineShare()
        {
            double share = 0.5;
            if (S.AutoBeans || HasRoaster) share += 0.25;
            if (HasCashier) share += 0.25;
            return share;
        }

        /// <summary>Back from away: research + the roaster catch up; the shop keeps earning by your setup (rate 50 %, cap 4 h;
        /// Offline Plus 75 % / 10 h). Null when nothing was earned.</summary>
        public OfflineSummary Offline(long now)
        {
            if (S.LastSeen <= 0) { S.LastSeen = now; return null; }
            double away = now - S.LastSeen;
            S.LastSeen = now;
            if (away < Gd.OfflineMinS) return null;
            TickResearch(away);
            TickRoast(away);
            bool plus = Perk("offline_plus");
            double capH = plus ? Gd.OfflineCapHPlus : Gd.OfflineCapH;
            double hours = Math.Min(away / 3600.0, capH);
            double amount = Math.Round(Math.Max(0, AvgNet()) * hours * Gd.OfflineDaysPerHour * (plus ? Gd.OfflineRatePlus : Gd.OfflineRate) * OfflineShare());
            if (amount <= 0) return null;
            S.Cash += amount;
            S.OfflinePending = amount;
            return new OfflineSummary { Seconds = away, Hours = hours, Amount = amount, Capped = away / 3600.0 > capH };
        }

        // ------------------------------------------------------------------------------------------------ rewards (sim side)

        public static readonly string[] RewardPlacements = GameData.RewardIds;
        public static bool RewardIsCash(string placement) => placement == "offline_x2" || placement == "daily_bonus";

        public bool MilkLow => S.Milk[0] < 2 || S.Milk[1] < 1;
        public bool IceLow => IceCap > 0 && S.Ice < 2;

        /// <summary>The crisis "last resort" can soften: grinder jam, a late milk van, a review spree, frost at origin.</summary>
        public string HalvableCrisis()
        {
            var v = Venue;
            if (v != null && v.Jammed) return "grinder_jam";
            if (S.MilkArriveAt > S.Minute && S.Phase == Phases.Live) return "milk_late";
            if (S.Events.Exists(e => e.Id == "review_spree")) return "review_spree";
            if (S.Events.Exists(e => e.Id == "frost")) return "frost";
            return "";
        }

        public RewardOfferInfo RewardOffer(string placement, long now)
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
                case "extra_barista":
                    if (S.Phase != Phases.Live || HelperOn) { o.Reason = "context"; return o; }
                    break;
                case "auto_dialin":
                    if (S.Phase != Phases.Live || AutoTune) { o.Reason = "context"; return o; }
                    break;
                case "emergency_stock":
                    if (S.Phase == Phases.Report || !(MilkLow || IceLow)) { o.Reason = "context"; return o; }
                    break;
                case "crisis_halve":
                    if (HalvableCrisis() == "") { o.Reason = "context"; return o; }
                    break;
                case "daily_bonus":
                    if (S.Phase == Phases.Report) { o.Reason = "context"; return o; }
                    o.Amount = Math.Round(Math.Min(def.Days * DayIncome(), single)); break;
            }
            o.Ok = true;
            return o;
        }

        public RewardResult GrantReward(string placement, long now)
        {
            var r = new RewardResult();
            var o = RewardOffer(placement, now);
            if (!o.Ok) { r.Reason = o.Reason; return r; }
            var def = Gd.Rewards[placement];
            switch (placement)
            {
                case "offline_x2": S.Cash += o.Amount; S.OfflinePending = 0; r.Amount = o.Amount; break;
                case "daily_bonus": S.Cash += o.Amount; S.Today.Bonus += o.Amount; r.Amount = o.Amount; break;
                case "extra_barista": S.HelperUntil = S.Minute + def.Minutes; break;
                case "auto_dialin": S.AutoTuneUntil = S.Minute + def.Minutes; if (Venue != null) Venue.Tune = 100; break;
                case "emergency_stock":
                    S.Milk[0] += Gd.RunL[0]; S.Milk[1] += Gd.RunL[1];
                    if (IceCap > 0) S.Ice += Gd.IceBagKg;
                    break;
                case "crisis_halve":
                {
                    string c = HalvableCrisis();
                    r.Reason = c;
                    if (c == "grinder_jam") { Venue.Jammed = false; Venue.JamUntil = -1; }
                    else if (c == "milk_late") S.MilkArriveAt = S.Minute;
                    else if (c == "review_spree") S.Events.RemoveAll(e => e.Id == "review_spree");
                    else if (c == "frost") foreach (var e in S.Events) if (e.Id == "frost") e.Mult = 1 + (e.Mult - 1) * 0.5;
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

        /// <summary>The guide finished or was skipped: random customers, crises and penalties may start (after the free days).</summary>
        public void EndTutorial() { S.Tutorial = false; HoldShelf = false; }
    }
}
