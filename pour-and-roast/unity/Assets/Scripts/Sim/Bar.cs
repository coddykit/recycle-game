// Pour & Roast sim core — the bar (design §4 A). Every ticket on the rail is a list of items; every item is a short list of
// tasks (shot at an espresso group head, steam at a steam wand, pour by any hand, pastry plating). Baristas stand at a
// station (espresso / steam / any) and take the next task the player's rule puts first (FIFO, deadline first, VIP
// first; a flagged ticket always leads). Equipment caps the parallel work: shots ≤ group heads, steams ≤ wands. Batching
// pulls the same drink from the next tickets onto one shot (2 cups in 1.5× the time). Dial-in drifts (bitter shots), the
// grinder wears and may jam (after the guide + the free days). Finished tickets wait on the pickup shelf; customers take
// them after a short hand-over (a cashier makes it quick; a tap hands everything over at once with a small tip).
// No stuck tickets: a walk-in whose ticket is not ready by their patience leaves (a 1★ review), a pre-order never
// leaves but a late one costs stars; at closing every open ticket is closed.
using System;
using System.Collections.Generic;

namespace PourAndRoast.Sim
{
    public sealed partial class Simulation
    {
        public const int HelperId = -2;
        readonly List<TicketState> order = new List<TicketState>();
        readonly List<int> freeWorkers = new List<int>();
        int shotsBusy, steamsBusy;

        // ------------------------------------------------------------------------------------------------ queries

        public IEnumerable<StaffState> StaffHere()
        {
            foreach (var s in S.Staff) if (s.Venue == S.Venue) yield return s;
        }

        public int CountRole(string role, string venue = null)
        {
            int n = 0;
            foreach (var s in S.Staff) if (s.Role == role && (venue == null || s.Venue == venue)) n++;
            return n;
        }

        public bool HelperOn => S.HelperUntil > S.Minute && S.Phase == Phases.Live;
        public bool HasCashier => CountRole(Roles.Cashier, S.Venue) > 0;
        public bool HasRunner => CountRole(Roles.Runner, S.Venue) > 0;
        public bool HasRoaster => CountRole(Roles.Roaster) > 0;
        public bool AutoTune => Perk("automation") || S.AutoTuneUntil > S.Minute && S.Phase == Phases.Live;
        public bool OffTune => Venue != null && Venue.Tune < Gd.TuneOff;
        public bool Jammed => Venue != null && Venue.Jammed;

        StaffState StaffById(int id) => S.Staff.Find(s => s.Id == id);

        /// <summary>Work speed of a worker (1 = the data's base times).</summary>
        public double SpeedOf(int id)
        {
            if (id == HelperId) return 1.05;
            var s = StaffById(id);
            if (s == null) return 1;
            double t = Gd.Traits.TryGetValue(s.Trait, out var tr) ? tr.Speed : 1;
            return t * (1 + Gd.LevelSpeed * (s.Level - 1));
        }

        double QBonusOf(int id)
        {
            if (id == HelperId) return 0;
            var s = StaffById(id);
            if (s == null) return 0;
            return (Gd.Traits.TryGetValue(s.Trait, out var tr) ? tr.Q : 0) + Gd.LevelQ * (s.Level - 1);
        }

        double TipBonusOf()
        {
            double b = 0;
            foreach (var s in StaffHere()) if (Gd.Traits.TryGetValue(s.Trait, out var tr)) b = Math.Max(b, tr.Tip);
            return b;
        }

        public int ShotsBusy => shotsBusy;
        public int SteamsBusy => steamsBusy;
        public int ReadyCount { get { int n = 0; foreach (var t in S.Tickets) if (t.Ready) n++; return n; } }

        double TaskMinutes(string task, MenuItemDef d)
        {
            double menu = 1 + Gd.MenuSlow * Math.Max(0, MenuCount - Gd.MenuFree);
            switch (task)
            {
                case Tasks.Shot:
                    return Gd.ShotMin * menu * (Has("tamping_mat") ? 0.92 : 1.0) * (Venue != null && Venue.Grinder >= 2 ? 1.0 - 0.06 * (Venue.Grinder - 1) : 1.0);
                case Tasks.Steam:
                    return Gd.SteamMin * menu * (Has("milk_pitchers") ? 0.9 : 1.0) * (Has("double_boiler") ? 0.9 : 1.0);
                case Tasks.Pour:
                    return (d != null && d.PourMin > 0 ? d.PourMin : Gd.PourMin) * menu;
                default:
                    return Gd.PastryMin;
            }
        }

        // ------------------------------------------------------------------------------------------------ supplies

        void StepSupplies(double dt)
        {
            var v = Venue;
            // milk delivery (on time at opening, or late) and the emergency run
            if (S.MilkArriveAt >= 0 && S.Minute >= S.MilkArriveAt)
            {
                S.Milk[0] += S.MilkPending[0]; S.Milk[1] += S.MilkPending[1];
                S.MilkPending[0] = S.MilkPending[1] = 0;
                S.MilkArriveAt = -1;
                Emit("milk_arrived");
            }
            if (S.RunArriveAt >= 0 && S.Minute >= S.RunArriveAt)
            {
                S.Milk[0] += S.RunPending[0]; S.Milk[1] += S.RunPending[1];
                S.RunPending[0] = S.RunPending[1] = 0;
                S.RunArriveAt = -1;
                Emit("milk_arrived", 1);
            }
            // the ice maker
            if (S.Ice < IceCap) S.Ice = Math.Min(IceCap, S.Ice + IcePerHour * dt / 60.0);
            if (v == null) return;
            // dial-in drift; the automation keeps the grinder tuned
            double drift = Gd.TuneDriftH * dt / 60.0 * WeatherDef.Tune * (Has("dial_scale") ? Gd.TuneScaleMult : 1.0);
            v.Tune = Math.Max(0, v.Tune - drift);
            if (AutoTune && v.Tune < Gd.TuneAutoFloor) v.Tune = 100;
            // a jammed grinder clears itself after a while (a tap fixes it at once)
            if (v.Jammed && S.Minute >= v.JamUntil) { v.Jammed = false; v.JamUntil = -1; Emit("jam_cleared"); }
        }

        // ------------------------------------------------------------------------------------------------ work

        void StepBar(double dt)
        {
            OrderTickets();
            // 1. progress
            foreach (var t in S.Tickets)
                foreach (var it in t.Items)
                {
                    if (it.Worker == -1) continue;
                    if (it.Worker != HelperId && StaffById(it.Worker) == null || it.Worker == HelperId && !HelperOn) { it.Worker = -1; continue; }   // left / helper gone: back to the queue
                    double rate = SpeedOf(it.Worker) / (it.Batched ? Gd.BatchMult : 1.0);
                    it.Left -= dt * rate;
                    if (it.Left <= 1e-9) FinishTask(t, it);
                }
            RecountBusy();
            // 2. assign free hands
            AssignWork();
            // 3. tickets: ready, hand-over, walk-outs
            for (int i = S.Tickets.Count - 1; i >= 0; i--)
            {
                var t = S.Tickets[i];
                if (!t.Ready && AllDone(t))
                {
                    t.Ready = true;
                    t.ReadyAt = S.Minute;
                    Emit("ready", t.Id);
                }
                if (t.Ready)
                {
                    if (AllFailed(t)) { Leave(i, true); continue; }
                    if (HoldShelf) continue;
                    double delay = HasCashier ? Gd.HandoverHostMin : Gd.HandoverMin;
                    double at = Math.Max(t.ReadyAt + delay, t.Pre ? t.Due : 0);
                    if (S.Minute >= at - 1e-9) Handover(i, false);
                }
                else if (!t.Pre && !S.Tutorial && S.Minute - t.Arrived > Gd.Customers[t.Cust].Patience) Leave(i, false);
            }
        }

        readonly List<int> busyShot = new List<int>(), busySteam = new List<int>();

        /// <summary>Heads / wands in use = distinct workers on a shot / a steam (both cups of a batch share one head).</summary>
        void RecountBusy()
        {
            busyShot.Clear(); busySteam.Clear();
            foreach (var t in S.Tickets)
                foreach (var it in t.Items)
                {
                    if (it.Worker == -1) continue;
                    string task = NextTask(it);
                    if (task == Tasks.Shot && !busyShot.Contains(it.Worker)) busyShot.Add(it.Worker);
                    else if (task == Tasks.Steam && !busySteam.Contains(it.Worker)) busySteam.Add(it.Worker);
                }
            shotsBusy = busyShot.Count;
            steamsBusy = busySteam.Count;
        }

        bool AllDone(TicketState t)
        {
            foreach (var it in t.Items) if (!ItemDone(it)) return false;
            return true;
        }

        bool AllFailed(TicketState t)
        {
            foreach (var it in t.Items) if (!it.Failed) return false;
            return true;
        }

        void FinishTask(TicketState t, ItemState it)
        {
            it.Stage++;
            it.Worker = -1;
            it.Left = 0;
            it.Batched = false;
        }

        /// <summary>The rail in working order: flagged first, then the player's rule (FIFO / deadline / VIP), then arrival.</summary>
        void OrderTickets()
        {
            order.Clear();
            foreach (var t in S.Tickets) if (!t.Ready) order.Add(t);
            string rule = S.Rule;
            bool autoFlag = Perk("automation") || Has("head_barista");
            if (autoFlag) foreach (var t in order) if (t.Pre && !t.Flag && t.Due - S.Minute <= 10) t.Flag = true;
            order.Sort((a, b) =>
            {
                if (a.Flag != b.Flag) return a.Flag ? -1 : 1;
                if (rule == Rules.Deadline) { int c = a.Due.CompareTo(b.Due); if (c != 0) return c; }
                else if (rule == Rules.Vip)
                {
                    bool va = Gd.Customers[a.Cust].Vip, vb = Gd.Customers[b.Cust].Vip;
                    if (va != vb) return va ? -1 : 1;
                }
                int d = a.Arrived.CompareTo(b.Arrived);
                return d != 0 ? d : a.Id.CompareTo(b.Id);
            });
        }

        /// <summary>The order the bar will work the open tickets in (the rail UI shows it).</summary>
        public List<TicketState> WorkOrder()
        {
            OrderTickets();
            return new List<TicketState>(order);
        }

        bool Busy(int worker)
        {
            foreach (var t in S.Tickets) foreach (var it in t.Items) if (it.Worker == worker) return true;
            return false;
        }

        void AssignWork()
        {
            freeWorkers.Clear();
            foreach (var s in S.Staff)
                if (s.Venue == S.Venue && (s.Role == Roles.Barista || s.Role == Roles.Cashier) && !Busy(s.Id)) freeWorkers.Add(s.Id);
            if (HelperOn && !Busy(HelperId)) freeWorkers.Add(HelperId);
            if (freeWorkers.Count == 0 || order.Count == 0) return;
            bool espressoStaffed = false, milkStaffed = false;
            foreach (var s in StaffHere())
            {
                if (s.Role != Roles.Barista) continue;
                if (s.Station == Stations.Espresso) espressoStaffed = true;
                if (s.Station == Stations.Milk) milkStaffed = true;
            }
            foreach (int w in freeWorkers)
            {
                var s = w == HelperId ? null : StaffById(w);
                string role = s != null ? s.Role : Roles.Barista;
                string station = s != null ? s.Station : Stations.Any;
                string primary = role == Roles.Cashier ? Tasks.Pastry : station == Stations.Espresso ? Tasks.Shot : station == Stations.Milk ? Tasks.Steam : "";
                if (primary != "" && TryAssign(w, role, station, primary, espressoStaffed, milkStaffed)) continue;
                if (role == Roles.Cashier) continue;
                TryAssign(w, role, station, "", espressoStaffed, milkStaffed);
            }
        }

        bool Allowed(string role, string station, string task, bool espressoStaffed, bool milkStaffed)
        {
            if (role == Roles.Cashier) return task == Tasks.Pastry;
            if (task == Tasks.Pastry) return true;
            if (station == Stations.Espresso) return task != Tasks.Steam || !milkStaffed;
            if (station == Stations.Milk) return task != Tasks.Shot || !espressoStaffed;
            return true;
        }

        bool TryAssign(int w, string role, string station, string only, bool espressoStaffed, bool milkStaffed)
        {
            var v = Venue;
            for (int oi = 0; oi < order.Count; oi++)
            {
                var t = order[oi];
                foreach (var it in t.Items)
                {
                    if (it.Worker != -1) continue;
                    string task = NextTask(it);
                    if (task == "") continue;
                    if (only != "" && task != only) continue;
                    if (!Allowed(role, station, task, espressoStaffed, milkStaffed)) continue;
                    if (task == Tasks.Shot && (v == null || v.Jammed || shotsBusy >= v.Heads)) continue;
                    if (task == Tasks.Steam && (v == null || steamsBusy >= v.Wands)) continue;
                    if (StartTask(w, t, it, task))
                    {
                        if (task == Tasks.Shot && S.Batch) TryBatch(w, oi, it);
                        return true;
                    }
                }
            }
            return false;
        }

        void TryBatch(int w, int fromIndex, ItemState first)
        {
            int look = 0;
            for (int oi = fromIndex; oi < order.Count && look <= Gd.BatchLook; oi++, look++)
                foreach (var it in order[oi].Items)
                {
                    if (it == first || it.Worker != -1 || it.M != first.M || NextTask(it) != Tasks.Shot) continue;
                    if (!StartTask(w, order[oi], it, Tasks.Shot, true)) return;
                    first.Batched = it.Batched = true;
                    first.Q -= 2; it.Q -= 2;
                    shotsBusy--;   // the second cup shares the first one's group head
                    return;
                }
        }

        /// <summary>Starts a task: takes the stock it needs (beans, milk, ice, a pastry) or fails the item. True = work started.</summary>
        bool StartTask(int w, TicketState t, ItemState it, string task, bool batched = false)
        {
            var d = ItemDef(it);
            var v = Venue;
            switch (task)
            {
                case Tasks.Shot:
                {
                    if (!TakeBeans(d.Beans)) { Fail(it, "beans"); return false; }
                    S.Today.Shots++;
                    double q = S.BeanQ + QBonusOf(w) + (Has("shot_timer") ? 3 : 0);
                    if (v != null && v.Tune < Gd.TuneOff) q -= (Gd.TuneOff - v.Tune) * Gd.TunePenalty;
                    it.Q = q;
                    Wear(v);
                    shotsBusy++;
                    break;
                }
                case Tasks.Steam:
                {
                    if (!TakeMilk(it)) { Fail(it, "milk"); return false; }
                    it.Q += SupplierQ() * 0.5;
                    steamsBusy++;
                    break;
                }
                case Tasks.Pour:
                {
                    if (d.Iced)
                    {
                        if (S.Ice < Gd.IcePerDrink) { Fail(it, "ice"); return false; }
                        if (d.Milk && !TakeMilk(it)) { Fail(it, "milk"); return false; }
                        S.Ice -= Gd.IcePerDrink;
                    }
                    if (!d.HasShot)
                    {
                        if (!TakeBeans(d.Beans)) { Fail(it, "beans"); return false; }
                        it.Q = S.BeanQ + QBonusOf(w) + (d.Showcase ? Gd.PourOverBonus : -4);
                    }
                    break;
                }
                case Tasks.Pastry:
                {
                    if (it.M < 0 || it.M >= S.Pastry.Length || S.Pastry[it.M] <= 0) { Fail(it, "sold_out"); return false; }
                    S.Pastry[it.M]--;
                    it.Q = PastryQuality;
                    break;
                }
            }
            it.Worker = w;
            it.Left = TaskMinutes(task, d);
            it.Batched = batched;
            return true;
        }

        void Fail(ItemState it, string why)
        {
            it.Failed = true;
            it.Worker = -1;
            S.Today.Failed++;
            Emit("item_failed", it.M, -1, 0, why);
        }

        double SupplierQ() => Gd.Suppliers.TryGetValue(S.Supplier, out var s) ? s.Q : 0;

        bool TakeBeans(double kg)
        {
            if (kg <= 0) return true;
            if (S.BeanKg < kg && S.AutoBeans) BuySupplierBeans(true);
            if (S.BeanKg < kg) return false;
            S.BeanKg -= kg;
            return true;
        }

        bool TakeMilk(ItemState it)
        {
            int k = it.Milk == 2 ? 1 : 0;
            double need = Gd.MilkPerDrink;
            if (k == 0) S.Today.MilkUsedW += need; else S.Today.MilkUsedO += need;   // demand, also when it runs out
            if (S.Milk[k] >= need) { S.Milk[k] -= need; return true; }
            int other = 1 - k;
            if (S.Milk[other] >= need) { S.Milk[other] -= need; it.Sub = true; return true; }
            return false;
        }

        void Wear(VenueState v)
        {
            if (v == null) return;
            double mult = (Has("burr_set") ? 0.5 : 1.0) * (v.Grinder >= 3 ? 0.6 : v.Grinder == 2 ? 0.8 : 1.0);
            v.Wear = Math.Min(100, v.Wear + Gd.WearPerShot * mult);
            if (!v.Jammed && v.Wear >= Gd.JamFrom && CrisesAllowed && Rng.Frand() < Gd.JamPerShot) Jam("wear");
        }

        public bool Jam(string why)
        {
            var v = Venue;
            if (v == null || v.Jammed) return false;
            v.Jammed = true;
            v.JamUntil = S.Minute + Gd.JamAutoFixMin;
            Emit("crisis", -1, -1, 0, "grinder_jam");
            return true;
        }

        // ------------------------------------------------------------------------------------------------ hand-over + reviews

        void Handover(int index, bool tapped)
        {
            var t = S.Tickets[index];
            var cd = Gd.Customers[t.Cust];
            double value = 0;
            double qsum = 0; int qn = 0, failed = 0; bool sub = false;
            foreach (var it in t.Items)
            {
                var d = ItemDef(it);
                if (it.Failed || d == null) { failed++; continue; }
                double q = Math.Max(0, Math.Min(100, it.Q));
                value += d.Price * VenueDef.PriceMult * (Gd.PriceQMin + Gd.PriceQSpan * q / 100.0);
                qsum += q; qn++;
                S.Today.QSum += q;
                if (it.Sub) sub = true;
            }
            double wait = S.Minute - t.Arrived;
            bool late = t.Pre && t.ReadyAt > t.Due + 2;
            string reason;
            double score = Review(t, qn > 0 ? qsum / qn : 0, failed, sub, late, wait, out reason);
            double tip = value * (cd.Tip + TipBonusOf()) * Math.Max(0, (score - 3) / 2) + (tapped ? value * Gd.TapTip : 0);
            value = Math.Round(value, 2);
            tip = Math.Round(tip, 2);
            S.Cash += value + tip;
            S.Today.Revenue += value;
            S.Today.Tips += tip;
            S.Today.Served++;
            S.Today.WaitSum += wait;
            if (late) S.Today.Late++;
            S.Served++;
            PostReview(t.Cust, score, reason);
            S.Tickets.RemoveAt(index);
            Emit("served", t.Id, t.Cust, value + tip, reason);
        }

        void Leave(int index, bool allFailed)
        {
            var t = S.Tickets[index];
            S.Today.Walked++;
            PostReview(t.Cust, 1, allFailed ? "sold_out" : "walked");
            S.Tickets.RemoveAt(index);
            Emit("walked", t.Id, t.Cust, 0, allFailed ? "sold_out" : "walked");
        }

        double Review(TicketState t, double q, int failed, bool sub, bool late, double wait, out string reason)
        {
            var cd = Gd.Customers[t.Cust];
            double score = 5;
            double worst = 0; string why0 = "great";
            void Pen(double p, string why) { score -= p; if (p > worst) { worst = p; why0 = why; } }
            if (!t.Pre)
            {
                double r = wait / Math.Max(1, cd.Patience);
                int k = 0;
                for (int i = 0; i < Gd.ReviewWait.Length; i++) if (r > Gd.ReviewWait[i]) k = i + 1;
                if (k < Gd.ReviewWaitPen.Length) Pen(Gd.ReviewWaitPen[k], "slow");
            }
            if (late) Pen(Gd.LatePen, "late");
            if (q > 0)
            {
                int k = 0;
                for (int i = 0; i < Gd.ReviewQ.Length; i++) if (q < Gd.ReviewQ[i]) k = i + 1;
                if (k < Gd.ReviewQPen.Length) Pen(Gd.ReviewQPen[k], OffTune ? "bitter" : "weak");
            }
            if (failed > 0) Pen(Gd.FailPen * Math.Min(2, failed), "sold_out");
            if (sub) Pen(Gd.SubPen, "no_oat");
            score += Gd.DecorReview * (Venue != null ? Venue.Decor : 0) + (Has("music_system") ? Gd.MusicReview : 0);
            score = Math.Max(1, Math.Min(5, score));
            reason = worst <= 0.01 ? (score >= 4.6 ? "great" : "good") : why0;
            return score;
        }

        void PostReview(int cust, double score, string reason)
        {
            S.Today.ReviewN++;
            S.Today.ReviewSum += score;
            S.Reviews.Add(new ReviewCard { Day = S.Day, Cust = cust, Stars = Math.Max(1, Math.Min(5, (int)Math.Round(score))), Reason = reason });
            while (S.Reviews.Count > Gd.WallSize) S.Reviews.RemoveAt(0);
            if (score <= 2.0) S.LowStreak++; else S.LowStreak = 0;
            if (S.LowStreak >= Gd.SpreeCount && CrisesAllowed && !S.Events.Exists(e => e.Id == "review_spree"))
            {
                S.LowStreak = 0;
                Fire("review_spree");
            }
        }

        // ------------------------------------------------------------------------------------------------ player actions (bar)

        /// <summary>"Make this one next": the ticket starts at a free espresso head now (or leads the rail). "" = ok.</summary>
        public string Brew(int ticketId)
        {
            var t = Ticket(ticketId);
            if (t == null) return "invalid";
            if (t.Ready) return "ready";
            if (S.Phase != Phases.Live) return "phase";
            t.Flag = true;
            OrderTickets();
            RecountBusy();
            AssignWork();
            Emit("brew", t.Id);
            return "";
        }

        public string Flag(int ticketId)
        {
            var t = Ticket(ticketId);
            if (t == null || t.Ready) return "invalid";
            t.Flag = !t.Flag;
            return "";
        }

        /// <summary>Cancels a ticket that has not been started (a duplicate from the pickup app, a wrong order).</summary>
        public string Cancel(int ticketId)
        {
            int i = S.Tickets.FindIndex(t => t.Id == ticketId);
            if (i < 0) return "invalid";
            var t = S.Tickets[i];
            foreach (var it in t.Items) if (it.Stage > 0 || it.Worker != -1) return "started";
            S.Tickets.RemoveAt(i);
            Emit("cancelled", t.Id);
            return "";
        }

        /// <summary>The player hands every finished cup over now (+ a small tip for the personal touch). Returns tickets handed over.</summary>
        public int HandoverAll()
        {
            int n = 0;
            for (int i = S.Tickets.Count - 1; i >= 0; i--)
            {
                var t = S.Tickets[i];
                if (!t.Ready || AllFailed(t) || t.Pre && S.Minute < t.Due) continue;
                Handover(i, true);
                n++;
            }
            return n;
        }

        public string DialIn()
        {
            var v = Venue;
            if (v == null) return "invalid";
            v.Tune = 100;
            Emit("dialed_in");
            return "";
        }

        public string Repair()
        {
            var v = Venue;
            if (v == null || !v.Jammed) return "invalid";
            v.Jammed = false;
            v.JamUntil = -1;
            v.Wear = Math.Max(0, v.Wear - 30);
            Emit("jam_cleared", 1);
            return "";
        }

        public string Assign(int staffId, string station)
        {
            var s = StaffById(staffId);
            if (s == null || !Stations.Valid(station)) return "invalid";
            if (s.Role != Roles.Barista) return "role";
            s.Station = station;
            foreach (var t in S.Tickets) foreach (var it in t.Items) if (it.Worker == staffId && NextTask(it) != Tasks.Pour) { it.Worker = -1; it.Batched = false; }
            Emit("assigned", staffId, -1, 0, station);
            return "";
        }

        public string SetRule(string rule)
        {
            if (!Rules.Valid(rule)) return "invalid";
            S.Rule = rule;
            return "";
        }

        public string SetBatch(bool on) { S.Batch = on; return ""; }
    }
}
