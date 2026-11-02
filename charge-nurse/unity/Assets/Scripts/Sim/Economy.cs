// Charge Nurse sim core — the day cycle (handover, night team, report), the economy, purchases, upgrades on shift time,
// wings, offline pay and the sim side of rewarded placements (caps + cooldowns persisted in the career; the kit's
// RewardedController enforces its own too).
using System;
using System.Collections.Generic;

namespace ChargeNurse.Sim
{
    public sealed class OfflineSummary { public double Seconds, Hours, Amount; public bool Capped; }
    public sealed class RewardOfferInfo { public bool Ok; public double Amount; public string Reason = ""; }
    public sealed class RewardResult { public bool Ok; public double Amount; public string Reason = ""; }
    public sealed class BuyInfo { public bool Ok; public double Cost; public string Reason = ""; }

    public sealed partial class Simulation
    {
        // ------------------------------------------------------------------------------------------------ day cycle

        /// <summary>Plan -> live (07:00, the morning handover from the night team is automatic).</summary>
        public bool StartDay()
        {
            if (S.Phase != Phases.Plan) return false;
            S.Phase = Phases.Live;
            S.Paused = false;
            Emit("day_live", S.Day);
            return true;
        }

        /// <summary>The player hands the shift over in the window (18:30-19:00): a calm handover, the shift ends now.</summary>
        public string Handover()
        {
            if (!HandoverWindow) return "window";
            S.HandoverDone = true;
            EndDay();
            return "";
        }

        void EndDay()
        {
            S.Minute = Gd.DayEnd;
            var td = S.Today;
            // handover: calm = rested teams; rushed = tired teams and lost notes (delays, a second triage)
            td.Rushed = !S.HandoverDone && !S.Tutorial;
            foreach (var t in Teams.All) S.Fatigue[t] = td.Rushed ? (S.Fatigue.TryGetValue(t, out var f) ? f : 0) * Gd.RushedKeep : 0;
            if (td.Rushed) LoseNotes();
            // inspection day: clean beds and short waits are checked before the night team takes over
            if (S.Events.Exists(e => e.Id == "inspection"))
            {
                int dirty = S.Beds.FindAll(b => b.State == BedState.Dirty).Count;
                var ev = Def("inspection");
                if (dirty <= 1 && td.AvgWait <= Gd.StarWaits[1]) { td.Bonus += ev.Bonus; S.Cash += ev.Bonus; Emit("inspection", 1, -1, ev.Bonus); }
                else { double fine = Math.Round(ev.Bonus * 0.5); td.Fines += fine; S.Cash -= fine; Emit("inspection", 0, -1, fine); }
            }
            // ward nights are billed, then the night team runs the hospital by itself (reduced capacity, no crises)
            foreach (var p in S.Patients)
                if (p.Stage == Stage.Stay || p.Stage == Stage.WardReady)
                {
                    double n = Math.Round(Gd.WardNight * PayMult * (Has("ward_rooms") ? 1.25 : 1.0));
                    td.Ward += n; S.Cash += n; p.Nights++;
                }
            if (S.Ambulance != null) DecideAmbulance(DefaultAccept());
            night = true;
            try { for (int k = 0; k < Gd.NightMin; k++) Step(1.0); }
            finally { night = false; }
            if (S.Ambulance != null) DecideAmbulance(DefaultAccept());

            // costs
            td.Wages = 0;
            foreach (var t in Teams.Pool) td.Wages += Team(t) * (Gd.Team(t)?.Wage ?? 0);
            foreach (var r in S.Rooms) td.Wages += r.Techs * (Gd.Team(Teams.Tech)?.Wage ?? 0);
            td.Upkeep = 0;
            foreach (var b in S.Beds) td.Upkeep += b.Bay ? Gd.UpkeepOf("bay") : WardOf(b) == "icu" ? Gd.UpkeepOf("bed_icu") : Gd.UpkeepOf("bed");
            foreach (var r in S.Rooms) td.Upkeep += Gd.Rooms.TryGetValue(r.Kind, out var rd) ? rd.Upkeep : 0;
            S.Cash -= td.Wages + td.Upkeep;

            // stars + reputation (the guide and the first days can only raise it: a forgiving start)
            td.StarCare = StarsBelow(td.BadRate, Gd.StarCare);
            td.StarWaits = StarsBelow(td.AvgWait, Gd.StarWaits);
            td.StarFlow = StarsBelow(td.BlockedShare, Gd.StarFlow);
            double score = td.Stars / 9.0 * 100.0;
            double penalty = Math.Min(5, td.Lwbs * Gd.BadRep[0] + td.Worsened * Gd.BadRep[1] + td.Transferred * Gd.BadRep[2] + td.Diverted * Gd.DivertRep);
            double next = S.Reputation + Gd.RepAlpha * (score - S.Reputation) - penalty;
            if (S.Tutorial || S.Day <= Gd.RepFreeDays) next = Math.Max(S.Reputation, next);
            S.Reputation = Math.Max(0, Math.Min(100, next));
            // burnout: sustained overload makes sick calls likelier (never raise demands — plan §2)
            double cap = 0, bsy = 0;
            foreach (var t in Teams.All) { cap += S.CapMin.TryGetValue(t, out var c) ? c : 0; bsy += S.BusyMin.TryGetValue(t, out var b) ? b : 0; }
            double util = cap > 0 ? bsy / cap : 0;
            if (!S.Tutorial) S.Burnout = Math.Max(0, Math.Min(100, S.Burnout + (util > Gd.BurnoutLine ? Gd.BurnoutRise * (Has("quiet_hours") ? 0.6 : 1.0) : -Gd.BurnoutFall)));
            if (td.Stars > S.BestStars) S.BestStars = td.Stars;
            if (td.Stars >= 7) S.GoodDays++;
            S.History.Add(td);
            while (S.History.Count > 14) S.History.RemoveAt(0);
            S.Minute = Gd.DayStart;
            S.Phase = Phases.Report;
            Emit("day_done", S.Day, td.Stars, td.Net);
        }

        void LoseNotes()
        {
            var pool = S.Patients.FindAll(p => !p.Guided && (p.Stage == Stage.Bay || p.Stage == Stage.Test || p.Stage == Stage.Stay) && !p.NoteLost);
            for (int k = 0; k < Gd.RushedNotes && pool.Count > 0; k++)
            {
                var p = pool[Rng.RangeI(0, pool.Count - 1)];
                pool.Remove(p);
                p.NoteLost = true;
                if (p.Stage == Stage.Stay) p.Left += Gd.NoteDelayMin;
                else { p.Tag = -1; p.Stage = Stage.Triage; }   // the card is gone: a second triage
            }
            if (Gd.RushedNotes > 0) Emit("notes_lost");
        }

        static int StarsBelow(double x, double[] th)
        {
            // th = [1 star, 2 stars, 3 stars] upper bounds, decreasing
            int s = 0;
            for (int i = 0; i < th.Length; i++) if (x <= th[i] + 1e-12) s = i + 1;
            return s;
        }

        /// <summary>Report -> next morning's board.</summary>
        public bool NextDay()
        {
            if (S.Phase != Phases.Report) return false;
            S.Day++;
            S.Minute = Gd.DayStart;
            S.Today = new DayStats { Day = S.Day };
            S.CapMin.Clear();
            S.BusyMin.Clear();
            S.HandoverDone = false;
            S.Surge = false;
            S.SickTeam = "";
            S.NeighborUsed = false;
            S.FloatTeam = ""; S.FloatLeft = 0;
            foreach (var e in S.Events)
                if (e.Id == "norovirus" && e.UntilDay < S.Day)
                    foreach (var b in S.Beds) if (b.State == BedState.Closed) b.State = BedState.Dirty;
            S.Events.RemoveAll(e => e.UntilDay < S.Day);
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

        /// <summary>Consumable cash pack: days × the usual day income. Returns the amount.</summary>
        public double GrantCash(double days)
        {
            double a = Math.Round(Math.Max(0, days) * DayIncome());
            S.Cash += a;
            Emit("cash_pack", -1, -1, a);
            return a;
        }

        /// <summary>The starter offer's extra porter (above the team cap is never needed: capped).</summary>
        public void GrantPorter()
        {
            var d = Gd.Team(Teams.Porter);
            if (d != null && Team(Teams.Porter) < d.Max) S.Teams[Teams.Porter] = Team(Teams.Porter) + 1;
        }

        // ------------------------------------------------------------------------------------------------ purchases

        int NextSlot(int zone)
        {
            if (zone < 0) return -1;
            var used = new HashSet<int>();
            foreach (var b in S.Beds) if (b.Zone == zone) used.Add(b.Slot);
            for (int s = 0; s < Gd.Zones[zone].Slots; s++) if (!used.Contains(s)) return s;
            return -1;
        }

        int AddBed(int zone)
        {
            int slot = NextSlot(zone);
            if (slot < 0) return -1;
            S.Beds.Add(new Bed { Zone = zone, Slot = slot, Bay = Gd.Zones[zone].Kind == "bays", State = BedState.Clean });
            return S.Beds.Count - 1;
        }

        int BayZoneWithRoom()
        {
            foreach (var z in Gd.Zones) if (z.Kind == "bays" && ZoneOpen(z.Index) && NextSlot(z.Index) >= 0) return z.Index;
            return -1;
        }

        /// <summary>Price + whether it can be bought now. Ids: hire:&lt;team&gt;, tech:&lt;room&gt;, bay, bed:&lt;ward&gt;,
        /// room:&lt;xray|lab|ct|or&gt;, wing:&lt;id&gt;.</summary>
        public BuyInfo CanBuy(string id)
        {
            var r = new BuyInfo();
            if (string.IsNullOrEmpty(id)) { r.Reason = "invalid"; return r; }
            string kind = id, arg = "";
            int c = id.IndexOf(':');
            if (c >= 0) { kind = id.Substring(0, c); arg = id.Substring(c + 1); }
            switch (kind)
            {
                case "hire":
                {
                    var t = Gd.Team(arg);
                    if (t == null || Teams.Index(arg) < 0) { r.Reason = "invalid"; return r; }
                    if (Team(arg) >= t.Max) { r.Reason = "max"; return r; }
                    r.Cost = t.Price;
                    break;
                }
                case "tech":
                {
                    var room = RoomOf(arg);
                    if (room == null || arg == "or") { r.Reason = Gd.Rooms.ContainsKey(arg) && arg != "or" ? "not_built" : "invalid"; return r; }
                    if (room.Techs >= (Gd.Team(Teams.Tech)?.Max ?? 3)) { r.Reason = "max"; return r; }
                    r.Cost = Gd.Team(Teams.Tech)?.Price ?? 0;
                    break;
                }
                case "bay":
                    if (BayZoneWithRoom() < 0) { r.Reason = Unlocked("town") && Unlocked("city") ? "max" : "wing"; return r; }
                    r.Cost = Gd.Price("bay");
                    break;
                case "bed":
                {
                    int z = Gd.ZoneOfWard(arg);
                    if (z < 0) { r.Reason = "invalid"; return r; }
                    if (!ZoneOpen(z)) { r.Reason = "wing"; return r; }
                    if (NextSlot(z) < 0) { r.Reason = "max"; return r; }
                    r.Cost = Gd.Price(arg == "icu" ? "bed_icu" : "bed");
                    break;
                }
                case "room":
                {
                    if (!Gd.Rooms.TryGetValue(arg, out var rd)) { r.Reason = "invalid"; return r; }
                    if (Built(arg)) { r.Reason = "owned"; return r; }
                    int z = arg == "or" ? Gd.Zone("or") : Gd.TestZone(arg);
                    if (!ZoneOpen(z)) { r.Reason = "wing"; return r; }
                    r.Cost = rd.Cost;
                    break;
                }
                case "surge":
                    // the surge team (+1 nurse, +1 doctor for today) — only on a surge day (flu week, a crash)
                    if (S.Surge) { r.Reason = "owned"; return r; }
                    if (S.Phase == Phases.Report || !SurgeDay) { r.Reason = "context"; return r; }
                    r.Cost = Gd.Price("surge");
                    break;
                case "wing":
                {
                    var w = Gd.Wing(arg);
                    if (w == null) { r.Reason = "invalid"; return r; }
                    if (Unlocked(arg)) { r.Reason = "owned"; return r; }
                    if (w.Chapter > Chapter + 1) { r.Reason = "locked"; return r; }
                    if (S.Reputation < w.Reputation) { r.Reason = "reputation"; return r; }
                    r.Cost = w.Cost;
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
            switch (kind)
            {
                case "hire": S.Teams[arg] = Team(arg) + 1; break;
                case "surge": S.Surge = true; break;
                case "tech": RoomOf(arg).Techs++; break;
                case "bay": AddBed(BayZoneWithRoom()); break;
                case "bed": AddBed(Gd.ZoneOfWard(arg)); break;
                case "room":
                    S.Rooms.Add(new Room { Kind = arg, Zone = arg == "or" ? Gd.Zone("or") : Gd.TestZone(arg), Techs = arg == "or" ? 0 : 1 });
                    Emit("room", S.Rooms.Count - 1, -1, 0, arg);
                    break;
                case "wing":
                {
                    S.Wings.Add(arg);
                    var w = Gd.Wing(arg);
                    foreach (var kv in w.StartBeds) for (int i = 0; i < kv.Value; i++) AddBed(Gd.Zone(kv.Key));
                    Emit("wing", -1, -1, 0, arg);
                    break;
                }
            }
            Emit("bought", -1, -1, info.Cost, id);
            return "";
        }

        /// <summary>A day the forecast or a crash says "surge": the surge team can be called in.</summary>
        public bool SurgeDay => S.Events.Exists(e => e.Id == "flu_surge" || e.Id == "crash");

        public string SetQueue(string rule)
        {
            if (!QueueRules.Valid(rule)) return "invalid";
            S.Queue = rule;
            return "";
        }

        /// <summary>The diversion rule (research): divert unanswered ambulances when the ED is this full (0 = never).</summary>
        public string SetDivert(int pct)
        {
            if (!Has("diversion_rules")) return "research";
            if (pct != 0 && pct != 80 && pct != 90 && pct != 100) return "invalid";
            S.DivertAt = pct;
            return "";
        }

        // ------------------------------------------------------------------------------------------------ upgrades (shift time)

        public string CanResearch(string id)
        {
            if (!Gd.ResearchById.TryGetValue(id ?? "", out var r)) return "invalid";
            if (Has(id)) return "owned";
            if (S.Researching != "") return S.Researching == id ? "running" : "busy";
            foreach (var q in r.Requires) if (!Has(q)) return "requires";
            if (!string.IsNullOrEmpty(r.Wing) && !Unlocked(r.Wing)) return "wing";
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
            S.ResearchLeft = r.Minutes;
            Emit("research_start", -1, -1, r.Cost, id);
            return "";
        }

        void TickResearch(double gameMin)
        {
            if (S.Researching == "") return;
            S.ResearchLeft -= gameMin;
            if (S.ResearchLeft > 0) return;
            string id = S.Researching;
            S.Researching = "";
            S.ResearchLeft = 0;
            if (!S.Research.Contains(id)) S.Research.Add(id);
            Emit("research_done", -1, -1, 0, id);
        }

        // ------------------------------------------------------------------------------------------------ offline

        /// <summary>The time a save was written (unix seconds).</summary>
        public void Touch(long now) { S.LastSeen = now; }

        /// <summary>Back from away: the "night shift" runs the hospital by the rules at reduced efficiency (50 %, cap 4 h;
        /// Night Shift Plus 80 % / 12 h). Null when nothing was earned.</summary>
        public OfflineSummary Offline(long now)
        {
            if (S.LastSeen <= 0) { S.LastSeen = now; return null; }
            double away = now - S.LastSeen;
            S.LastSeen = now;
            if (away < Gd.OfflineMinS || S.Tutorial) return null;
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

        public static readonly string[] RewardPlacements = { "offline_x2", "float_nurse", "call_neighbor", "daily_bonus" };
        public static bool RewardIsCash(string placement) => placement == "offline_x2" || placement == "daily_bonus";

        /// <summary>Patients the neighbour hospital would take now (waiting, not yet with a doctor; lowest priority first).</summary>
        List<Patient> NeighborCandidates()
        {
            var l = S.Patients.FindAll(p => !p.Guided && !p.Seen && (p.Stage == Stage.Reg || p.Stage == Stage.Triage || p.Stage == Stage.Bay || p.Stage == Stage.Test));
            l.Sort((a, b) => { int c = Prio(a).CompareTo(Prio(b)); return c != 0 ? c : b.Id.CompareTo(a.Id); });
            return l;
        }

        /// <summary>The team the float staff joins: the one the bottleneck names, else nurses.</summary>
        public string FloatTarget()
        {
            var b = FindBottleneck();
            return b.Team != "" && b.Team != Teams.Tech ? b.Team : Teams.Nurse;
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
                case "float_nurse":
                    if (S.Phase != Phases.Live || S.FloatLeft > 0) { o.Reason = "context"; return o; }
                    break;
                case "call_neighbor":
                    // crisis last resort (plan §10): only while a crisis runs, once per crisis day, with a real queue
                    if (S.Phase != Phases.Live || S.NeighborUsed || ActiveCrises().Count == 0 || NeighborCandidates().Count < 3) { o.Reason = "context"; return o; }
                    o.Amount = Math.Min(def.Patients, NeighborCandidates().Count);
                    break;
                case "daily_bonus":
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
                case "daily_bonus": S.Cash += o.Amount; r.Amount = o.Amount; break;
                case "float_nurse":
                    S.FloatTeam = FloatTarget();
                    S.FloatLeft = def.Minutes > 0 ? def.Minutes : 60;
                    r.Amount = S.FloatLeft;
                    break;
                case "call_neighbor":
                {
                    var l = NeighborCandidates();
                    int n = Math.Min(def.Patients, l.Count);
                    for (int i = 0; i < n; i++) { S.Patients.Remove(l[i]); FreeBed(l[i]); Emit("neighbor", l[i].Id); }
                    S.NeighborUsed = true;
                    r.Amount = n;
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
    }
}
