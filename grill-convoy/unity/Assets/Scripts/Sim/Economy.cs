// Grill Convoy sim core — the day cycle, the economy, purchases, crew / menu / layout actions, research, pitches, offline
// pay and the sim side of rewarded placements (caps + cooldowns persisted in the career; the kit's RewardedController
// enforces its own too).
using System;
using System.Collections.Generic;

namespace GrillConvoy.Sim
{
    public sealed class OfflineSummary { public double Seconds, Hours, Amount; public bool Capped; }
    public sealed class RewardOfferInfo { public bool Ok; public double Amount; public string Reason = ""; }
    public sealed class RewardResult { public bool Ok; public double Amount; public string Reason = ""; }
    public sealed class BuyInfo { public bool Ok; public double Cost; public string Reason = ""; }

    public sealed partial class Simulation
    {
        // ------------------------------------------------------------------------------------------------ day cycle

        /// <summary>Plan -> live: every truck with a pitch drives out (a truck already standing at its pitch opens).</summary>
        public bool StartDay()
        {
            if (S.Phase != Phases.Plan) return false;
            S.Phase = Phases.Live;
            S.Paused = false;
            for (int ti = 0; ti < S.Trucks.Count; ti++)
            {
                var t = S.Trucks[ti];
                if (t.Pitch < 0 || t.Crew.Count == 0) continue;
                if (t.Status == TruckStatus.Service && t.AtNode == t.Pitch) { Emit("opened", ti, t.Pitch); continue; }
                if (t.Status == TruckStatus.Service) { StartLeg(t, t.Pitch); continue; }
                if (t.Status == TruckStatus.Idle) Send(ti);
            }
            Emit("day_live", S.Day);
            return true;
        }

        void EndDay()
        {
            S.Minute = Gd.DayEnd;
            S.HandStage = 0;
            // every truck comes home (remaining km charged), the line disperses, leftovers go back (night spoilage)
            for (int ti = 0; ti < S.Trucks.Count; ti++)
            {
                var t = S.Trucks[ti];
                if (t.Queue.Count > 0) DisperseLine(ti);
                if (t.Status == TruckStatus.Drive)
                {
                    int from = t.Path.Count > 0 ? t.Path[t.Path.Count - 1] : t.AtNode;
                    double rest = Math.Max(0, t.PathKm - t.PathPos) + (t.Target >= 0 ? Gd.Graph.Km(from, Gd.DepotNode) : 0);
                    if (rest < RoadGraph.Inf) ChargeKm(t, rest);
                }
                else if (t.Status == TruckStatus.Service && t.AtNode != Gd.DepotNode)
                {
                    double back = Gd.Graph.Km(t.AtNode, Gd.DepotNode);
                    if (back < RoadGraph.Inf) ChargeKm(t, back);
                }
                UnloadToCommissary(t, true);
                // propane refilled at the commissary for tomorrow
                double refill = (1 - t.Propane) * Gd.Svc.PropanePrice;
                S.Today.Propane += refill;
                S.Cash -= refill;
                t.Propane = 1;
                t.Status = TruckStatus.Idle; t.AtNode = Gd.DepotNode; t.Target = -1; t.Restocking = false; t.Path.Clear(); t.PathKm = t.PathPos = 0;
                t.FlareUntil = 0; t.GrillOffUntil = 0; t.AllHandsUntil = 0; t.AllHandsReady = 0; t.HelperUntil = 0; t.Work = 0;
            }
            // the night crew: par-level batches, then the prep queue runs
            AutoPrep();
            StepCommissary(Gd.NightPrepMin / Math.Max(0.1, PrepSpeed));

            var td = S.Today;
            td.Weather = S.Weather;
            foreach (var t in S.Trucks) td.Wages += t.Crew.Count * Gd.Wage;
            var paid = new HashSet<int>();
            foreach (var t in S.Trucks) if (t.OpenMin > 0 && t.Pitch >= 0 && paid.Add(t.Pitch)) td.Permits += NodeAt(t.Pitch).Permit;
            S.Cash -= td.Wages + td.Permits;
            td.StarService = StarsBelow(td.Arrivals <= 0 ? 1 : td.LostRate, Gd.StarService);
            // a crew that idled most of the day (quiet weekend at the offices, rain on the beach) earns at most 2 service stars:
            // nobody walked away, but the truck was in the wrong place ("try a busier pitch") — not after the forgiving start
            if (Gd.StarBusy > 0 && !S.Tutorial && S.Day > Gd.FameFreeDays && td.StarService > 2 && BusyShare() < Gd.StarBusy) td.StarService = 2;
            double wasteRate = td.Items + td.Waste <= 0 ? 0 : (double)td.Waste / (td.Items + td.Waste);
            td.StarStock = td.Served <= 0 ? 0 : Math.Min(StarsBelow(td.SelloutRate, Gd.StarStock), StarsBelow(wasteRate, new[] { 0.35, 0.2, 0.1 }));
            td.StarSpeed = td.Served <= 0 ? 0 : StarsBelow(td.AvgWait, Gd.StarSpeed);
            // pitch reputation: served share and wait at each pitch a truck worked today
            foreach (var t in S.Trucks)
            {
                if (t.OpenMin <= 0 || t.Pitch < 0) continue;
                var ps = PitchState(t.Pitch);
                double servedShare = t.Arrivals <= 0 ? 0.7 : (double)t.Served / t.Arrivals;
                double wait = t.Served <= 0 ? 10 : t.WaitSum / t.Served;
                double rating = 5 - 3 * Math.Min(1, wait / 10.0) + Gd.TierRating[t.Tier];
                double delta = Gd.RepGain * (servedShare - 0.7) + Gd.RepRating * (rating - 3.5) / 1.5;
                if (S.Tutorial || S.Day <= Gd.FameFreeDays) delta = Math.Max(0, delta);
                ps.Rep = Math.Max(0, Math.Min(100, ps.Rep + delta));
            }
            double score = td.Stars / 9.0 * 100.0;
            double next = S.Fame + Gd.FameAlpha * (score - S.Fame);
            // forgiving start: the guide and the first days can only raise Fame
            if (S.Tutorial || S.Day <= Gd.FameFreeDays) next = Math.Max(S.Fame, next);
            S.Fame = Math.Max(0, Math.Min(100, next));
            if (td.Stars > S.BestDayStars) S.BestDayStars = td.Stars;
            if (td.Stars >= 8) S.ThreeStarDays++;
            S.History.Add(td);
            while (S.History.Count > 14) S.History.RemoveAt(0);
            S.Forecast = RollWeather(S.Day + 1);
            S.Phase = Phases.Report;
            Emit("day_done", S.Day, td.Stars, td.Net);
        }

        /// <summary>Burgers made today / what the open trucks' lines could have made in their open minutes (0..1+).</summary>
        public double BusyShare()
        {
            double cap = 0, made = 0;
            foreach (var t in S.Trucks)
            {
                if (t.OpenMin <= 0) continue;
                cap += Line(t).Throughput * t.OpenMin;
                made += t.Items;
            }
            return cap <= 0 ? 1 : made / cap;
        }

        static int StarsBelow(double x, double[] th)
        {
            // th = [1 star, 2 stars, 3 stars] upper bounds, decreasing
            int s = 0;
            for (int i = 0; i < th.Length; i++) if (x <= th[i] + 1e-12) s = i + 1;
            return s;
        }

        /// <summary>Report -> next morning's plan. Trucks repack like yesterday (auto trucks: by the forecast).</summary>
        public bool NextDay()
        {
            if (S.Phase != Phases.Report) return false;
            S.Day++;
            S.Minute = Gd.DayStart;
            S.Weather = S.Forecast;
            S.Today = new DayStats { Day = S.Day, Weather = S.Weather };
            S.Events.RemoveAll(e => e.UntilDay < S.Day);
            for (int ti = 0; ti < S.Trucks.Count; ti++)
            {
                var t = S.Trucks[ti];
                t.KmToday = 0; t.Served = t.Walked = t.SoldOut = t.Arrivals = t.Items = 0; t.SelloutMin = t.OpenMin = t.WaitSum = t.Sales = 0; t.Promo = false;
                if (t.Auto) { AutoAssign(ti); ApplySuggestedPack(ti); }
                else Repack(ti);
            }
            PlanDay();
            S.Phase = Phases.Plan;
            S.Paused = false;
            Emit("day_start", S.Day);
            return true;
        }

        /// <summary>Head cook rule: crew roles follow the line's bottleneck (at most one runner).</summary>
        void AutoAssign(int ti)
        {
            var t = S.Trucks[ti];
            int n = t.Crew.Count;
            if (n == 0) return;
            var roles = new List<string> { Roles.Griller, Roles.Assembler, Roles.Cashier, Roles.Runner };
            t.Crew.Clear();
            for (int i = 0; i < n; i++)
            {
                if (i < 3) { t.Crew.Add(roles[i]); continue; }
                var L = Line(t);
                t.Crew.Add(L.Bottleneck == Stations.Grill ? Roles.Griller : L.Bottleneck == Stations.Assembly ? Roles.Assembler : Roles.Cashier);
            }
            if (n == 2)
            {
                var L = Line(t);
                if (L.Bottleneck == Stations.Window) t.Crew[1] = Roles.Cashier;
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

        static (string kind, string[] args) Split(string id)
        {
            var parts = (id ?? "").Split(':');
            var args = new string[Math.Max(0, parts.Length - 1)];
            Array.Copy(parts, 1, args, 0, args.Length);
            return (parts[0], args);
        }

        static int Arg(string[] a, int i) => a.Length > i && int.TryParse(a[i], out int v) ? v : -1;

        /// <summary>Price + whether it can be bought now. Ids: pitch:&lt;district&gt;, truck:&lt;type&gt;,
        /// station:&lt;truck&gt;:&lt;type&gt;, hire:&lt;truck&gt;:&lt;role&gt;, extend:&lt;truck&gt;.</summary>
        public BuyInfo CanBuy(string id)
        {
            var r = new BuyInfo();
            var (kind, a) = Split(id);
            switch (kind)
            {
                case "pitch":
                {
                    var dd = a.Length > 0 ? Gd.District(a[0]) : null;
                    if (dd == null) { r.Reason = "invalid"; return r; }
                    if (Unlocked(dd.Id)) { r.Reason = "owned"; return r; }
                    if (dd.Chapter > Chapter + 1) { r.Reason = "locked"; return r; }
                    if (S.Fame < dd.Fame) { r.Reason = "fame"; return r; }
                    r.Cost = dd.Cost;
                    break;
                }
                case "truck":
                {
                    if (a.Length == 0 || !Gd.Vehicles.TryGetValue(a[0], out var v) || v.Price <= 0) { r.Reason = "invalid"; return r; }
                    if (S.Trucks.Count >= Gd.MaxTrucks) { r.Reason = "max"; return r; }
                    if (Chapter < 3) { r.Reason = "chapter"; return r; }
                    r.Cost = v.Price;
                    break;
                }
                case "station":
                {
                    int ti = Arg(a, 0);
                    if (ti < 0 || ti >= S.Trucks.Count || a.Length < 2 || !Gd.StationDefs.TryGetValue(a[1], out var sd)) { r.Reason = "invalid"; return r; }
                    var t = S.Trucks[ti];
                    if (sd.Research != "" && !Has(sd.Research) && !(sd.Id == Stations.Window && CountStations(t, Stations.Window) == 0)) { r.Reason = "research"; return r; }
                    if (CountStations(t, sd.Id) >= sd.Max) { r.Reason = "max"; return r; }
                    if (t.Layout.Count >= GridW(t) * GridH(t)) { r.Reason = "no_room"; return r; }
                    r.Cost = sd.Price;
                    break;
                }
                case "hire":
                {
                    int ti = Arg(a, 0);
                    if (ti < 0 || ti >= S.Trucks.Count || a.Length < 2 || !Roles.Valid(a[1])) { r.Reason = "invalid"; return r; }
                    var t = S.Trucks[ti];
                    if (t.Crew.Count >= VehicleOf(t).Crew) { r.Reason = "max"; return r; }
                    r.Cost = Gd.HirePrice;
                    break;
                }
                case "extend":
                {
                    int ti = Arg(a, 0);
                    if (ti < 0 || ti >= S.Trucks.Count) { r.Reason = "invalid"; return r; }
                    var t = S.Trucks[ti];
                    var v = VehicleOf(t);
                    if (t.Extended || (v.WExt <= v.W && v.HExt <= v.H)) { r.Reason = "owned"; return r; }
                    if (!Has("body_extension")) { r.Reason = "research"; return r; }
                    r.Cost = Gd.ExtendPrice;
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
            var (kind, a) = Split(id);
            switch (kind)
            {
                case "pitch": S.Districts.Add(a[0]); Emit("district", -1, -1, 0, a[0]); break;
                case "truck":
                {
                    int nid = 0;
                    foreach (var t in S.Trucks) nid = Math.Max(nid, t.Id + 1);
                    var nt = NewTruck(nid, a[0]);
                    nt.Crew.Add(Roles.Griller);
                    nt.Crew.Add(Roles.Assembler);
                    S.Trucks.Add(nt);
                    Emit("truck_new", S.Trucks.Count - 1);
                    break;
                }
                case "station":
                {
                    var t = S.Trucks[Arg(a, 0)];
                    var (x, y) = FreeTile(t);
                    SaveUndo(t);
                    t.Layout.Add(new StationTile { Type = a[1], X = x, Y = y });
                    Emit("layout", Arg(a, 0));
                    break;
                }
                case "hire": S.Trucks[Arg(a, 0)].Crew.Add(a[1]); Emit("hired", Arg(a, 0), -1, 0, a[1]); break;
                case "extend": S.Trucks[Arg(a, 0)].Extended = true; Emit("layout", Arg(a, 0)); break;
            }
            Emit("bought", -1, -1, info.Cost, id);
            return "";
        }

        (int x, int y) FreeTile(TruckState t)
        {
            int w = GridW(t), h = GridH(t);
            for (int y = h - 1; y >= 0; y--)
                for (int x = 0; x < w; x++)
                    if (TileIndexAt(t, x, y) < 0) return (x, y);
            return (0, 0);
        }

        // ------------------------------------------------------------------------------------------------ crew / menu / tier

        public string SetRole(int truck, int i, string role)
        {
            if (truck < 0 || truck >= S.Trucks.Count || !Roles.Valid(role)) return "invalid";
            var t = S.Trucks[truck];
            if (i < 0 || i >= t.Crew.Count) return "invalid";
            t.Crew[i] = role;
            return "";
        }

        public string Release(int truck, int i)
        {
            if (truck < 0 || truck >= S.Trucks.Count) return "invalid";
            var t = S.Trucks[truck];
            if (i < 0 || i >= t.Crew.Count) return "invalid";
            if (t.Crew.Count <= 1) return "last";
            t.Crew.RemoveAt(i);
            return "";
        }

        public int MenuSlots => 3 + (Has("menu_board_plus") ? 1 : 0);
        public bool CardAvailable(CardDef c) => c != null && (c.Research == "" || Has(c.Research));

        /// <summary>Puts a card on the board or takes it off (≥ 1 card, ≤ the board's slots).</summary>
        public string ToggleCard(int truck, string card)
        {
            if (truck < 0 || truck >= S.Trucks.Count) return "invalid";
            var c = Gd.Card(card);
            if (c == null) return "invalid";
            var t = S.Trucks[truck];
            if (t.Menu.Contains(card))
            {
                if (t.Menu.Count <= 1) return "last";
                t.Menu.Remove(card);
                return "";
            }
            if (!CardAvailable(c)) return "research";
            if (t.Menu.Count >= MenuSlots) return "board_full";
            t.Menu.Add(card);
            return "";
        }

        public string SetTier(int truck, int tier)
        {
            if (truck < 0 || truck >= S.Trucks.Count || tier < 0 || tier > 2) return "invalid";
            if (tier == 2 && !Has("premium_tier")) return "research";
            S.Trucks[truck].Tier = tier;
            return "";
        }

        // ------------------------------------------------------------------------------------------------ layout editor

        void SaveUndo(TruckState t) => t.Undo = StationTile.CloneList(t.Layout);

        /// <summary>Moves the station on tile index `from` (y*w+x) to tile `to` (swaps when occupied). "" = ok.</summary>
        public string Move(int truck, int from, int to)
        {
            if (truck < 0 || truck >= S.Trucks.Count) return "invalid";
            var t = S.Trucks[truck];
            int w = GridW(t), h = GridH(t);
            if (from < 0 || to < 0 || from >= w * h || to >= w * h || from == to) return "invalid";
            int a = TileIndexAt(t, from % w, from / w);
            if (a < 0) return "empty";
            int b = TileIndexAt(t, to % w, to / w);
            SaveUndo(t);
            if (b >= 0) { t.Layout[b].X = from % w; t.Layout[b].Y = from / w; }
            t.Layout[a].X = to % w;
            t.Layout[a].Y = to / w;
            Emit("layout", truck);
            return "";
        }

        /// <summary>Sells a station back for half its price (a truck keeps at least one grill, bench and window).</summary>
        public string Remove(int truck, int tile)
        {
            if (truck < 0 || truck >= S.Trucks.Count) return "invalid";
            var t = S.Trucks[truck];
            int w = GridW(t);
            int i = TileIndexAt(t, tile % w, tile / w);
            if (i < 0) return "empty";
            string type = t.Layout[i].Type;
            if ((type == Stations.Grill || type == Stations.Assembly || type == Stations.Window) && CountStations(t, type) <= 1) return "last";
            SaveUndo(t);
            t.Layout.RemoveAt(i);
            if (Gd.StationDefs.TryGetValue(type, out var sd)) S.Cash += Math.Round(sd.Price * 0.5);
            Emit("layout", truck);
            return "";
        }

        public string UndoLayout(int truck)
        {
            if (truck < 0 || truck >= S.Trucks.Count) return "invalid";
            var t = S.Trucks[truck];
            if (t.Undo == null) return "nothing";
            // undo only restores positions of stations the truck still owns (a sold station stays sold)
            if (t.Undo.Count != t.Layout.Count) { t.Undo = null; return "nothing"; }
            var cur = StationTile.CloneList(t.Layout);
            t.Layout = t.Undo;
            t.Undo = cur;
            Emit("layout", truck);
            return "";
        }

        /// <summary>Line throughput plus the small side bonuses (fryer / wrap bench by the window) — what "better layout" means.</summary>
        public double LayoutScore(TruckState t) => Line(t).Throughput + (Adjacent(t, Stations.Fryer, Stations.Window) ? 0.02 : 0) + (Adjacent(t, Stations.Wrap, Stations.Window) ? 0.02 : 0);

        static readonly string[] AutoOrder = { Stations.Window, Stations.Assembly, Stations.Grill, Stations.Toaster, Stations.Sauce, Stations.Fridge, Stations.Fryer, Stations.Wrap };

        /// <summary>The "Auto-arrange" button (plan §13 risk 3: an auto-suggest for the phone layout editor): a greedy
        /// arrangement — windows on the front row, the bench behind them, grills next to the bench, toaster / sauce rail at
        /// the bench, fridge / fryer / wrap at the window. Kept only when it scores at least as well as the current one.</summary>
        public string AutoLayout(int truck)
        {
            if (truck < 0 || truck >= S.Trucks.Count) return "invalid";
            var t = S.Trucks[truck];
            int w = GridW(t), h = GridH(t);
            var old = StationTile.CloneList(t.Layout);
            double before = LayoutScore(t);
            var todo = StationTile.CloneList(t.Layout);
            todo.Sort((a, b) => Array.IndexOf(AutoOrder, a.Type).CompareTo(Array.IndexOf(AutoOrder, b.Type)));
            var placed = new List<StationTile>();
            int D(int x, int y, string type)
            {
                int best = 99;
                foreach (var p in placed) if (p.Type == type) best = Math.Min(best, Math.Abs(p.X - x) + Math.Abs(p.Y - y));
                return best;
            }
            foreach (var s in todo)
            {
                double bestScore = double.MinValue; int bx = 0, by = 0;
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        if (placed.Exists(p => p.X == x && p.Y == y)) continue;
                        double sc = 0;
                        switch (s.Type)
                        {
                            case Stations.Window: sc = (y == 0 ? 20 : 0) - Math.Abs(x - (w - 1) / 2.0); break;
                            case Stations.Assembly: sc = -5 * Math.Abs(Math.Min(D(x, y, Stations.Window), 9) - 1) - y * 0.1 - Math.Abs(x - (w - 1) / 2.0) * 0.2; break;
                            case Stations.Grill: sc = -5 * Math.Abs(Math.Min(D(x, y, Stations.Assembly), 9) - 1) + (D(x, y, Stations.Window) == 1 ? -2 : 0) + y * 0.3; break;
                            case Stations.Toaster: case Stations.Sauce: sc = D(x, y, Stations.Assembly) == 1 ? 10 : -D(x, y, Stations.Assembly); break;
                            default: sc = D(x, y, Stations.Window) == 1 ? 10 : -D(x, y, Stations.Window); break;
                        }
                        if (sc > bestScore) { bestScore = sc; bx = x; by = y; }
                    }
                placed.Add(new StationTile { Type = s.Type, X = bx, Y = by });
            }
            t.Layout = placed;
            if (LayoutScore(t) < before - 1e-9) { t.Layout = old; return "no_better"; }
            t.Undo = old;
            Emit("layout", truck);
            return "";
        }

        public int PresetSlots => Gd.Presets + (Perk("convoy_pass") ? Gd.PassPresets : 0);

        public string SavePreset(int truck, int slot)
        {
            if (truck < 0 || truck >= S.Trucks.Count || slot < 0 || slot >= PresetSlots) return "invalid";
            var t = S.Trucks[truck];
            while (t.Presets.Count <= slot) t.Presets.Add(new List<StationTile>());
            t.Presets[slot] = StationTile.CloneList(t.Layout);
            return "";
        }

        /// <summary>Loads a saved arrangement: stations are matched by type (missing ones stay where they are).</summary>
        public string LoadPreset(int truck, int slot)
        {
            if (truck < 0 || truck >= S.Trucks.Count || slot < 0 || slot >= PresetSlots) return "invalid";
            var t = S.Trucks[truck];
            if (slot >= t.Presets.Count || t.Presets[slot].Count == 0) return "empty";
            SaveUndo(t);
            var used = new HashSet<int>();
            var placed = new List<StationTile>();
            foreach (var p in t.Presets[slot])
            {
                int i = t.Layout.FindIndex(s => s.Type == p.Type && !used.Contains(t.Layout.IndexOf(s)));
                if (i < 0 || p.X >= GridW(t) || p.Y >= GridH(t)) continue;
                used.Add(i);
                placed.Add(new StationTile { Type = p.Type, X = p.X, Y = p.Y });
            }
            for (int i = 0; i < t.Layout.Count; i++)
            {
                if (used.Contains(i)) continue;
                var s = t.Layout[i];
                bool taken = placed.Exists(q => q.X == s.X && q.Y == s.Y);
                if (taken)
                {
                    int w = GridW(t), h = GridH(t);
                    for (int k = 0; k < w * h && taken; k++)
                        if (!placed.Exists(q => q.X == k % w && q.Y == k / w)) { s = new StationTile { Type = s.Type, X = k % w, Y = k / w }; taken = false; }
                }
                placed.Add(s.Clone());
            }
            t.Layout = placed;
            Emit("layout", truck);
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
            Emit("research_done", -1, -1, 0, id);
        }

        // ------------------------------------------------------------------------------------------------ offline

        /// <summary>The time a save was written (unix seconds).</summary>
        public void Touch(long now) { S.LastSeen = now; }

        /// <summary>Back from away: research catches up; the convoy keeps earning by its rules (rate 50 %, cap 4 h;
        /// Offline Plus 75 % / 10 h). Trucks on Dispatcher rules count fully, the others a quarter. Null when nothing.</summary>
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
            int auto = AutoCount;
            double share = S.Trucks.Count == 0 ? 0 : Math.Max(0.25, (double)auto / S.Trucks.Count);
            double amount = Math.Round(Math.Max(0, AvgNet()) * hours * Gd.OfflineDaysPerHour * (plus ? Gd.OfflineRatePlus : Gd.OfflineRate) * share);
            if (amount <= 0) return null;
            S.Cash += amount;
            S.OfflinePending = amount;
            return new OfflineSummary { Seconds = away, Hours = hours, Amount = amount, Capped = away / 3600.0 > capH };
        }

        // ------------------------------------------------------------------------------------------------ rewards (sim side)

        public static readonly string[] RewardPlacements = { "offline_x2", "extra_runner", "bonus_patties", "quick_prep", "crisis_rescue", "daily_bonus" };
        public static bool RewardIsCash(string placement) => placement == "offline_x2" || placement == "daily_bonus";

        int FirstTruck(Predicate<TruckState> ok)
        {
            for (int i = 0; i < S.Trucks.Count; i++) if (ok(S.Trucks[i])) return i;
            return -1;
        }

        bool Troubled(TruckState t) => t.Open && (t.Flaring || t.Propane <= 0);

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
            TruckState tt = target >= 0 && target < S.Trucks.Count ? S.Trucks[target] : null;
            switch (placement)
            {
                case "offline_x2":
                    if (S.OfflinePending <= 0) { o.Reason = "context"; return o; }
                    o.Amount = Math.Round(Math.Min(S.OfflinePending, single)); break;
                case "extra_runner":
                    if (S.Phase != Phases.Live || (tt != null ? !tt.Open || tt.HelperUntil > S.Minute : FirstTruck(x => x.Open && x.HelperUntil <= S.Minute) < 0)) { o.Reason = "context"; return o; }
                    break;
                case "bonus_patties":
                    if (S.Phase == Phases.Report || (tt != null ? tt.Load[Stock.Patty] + def.Amount > TruckCap(tt, Stock.Patty) : FirstTruck(x => x.Load[Stock.Patty] + def.Amount <= TruckCap(x, Stock.Patty)) < 0)) { o.Reason = "context"; return o; }
                    o.Amount = def.Amount; break;
                case "quick_prep":
                    if (S.Commissary.Queue.Count == 0 || S.Phase == Phases.Report) { o.Reason = "context"; return o; }
                    break;
                case "crisis_rescue":
                    if (tt != null ? !Troubled(tt) : FirstTruck(Troubled) < 0) { o.Reason = "context"; return o; }
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
            var def = Gd.Rewards[placement];
            switch (placement)
            {
                case "offline_x2": S.Cash += o.Amount; S.OfflinePending = 0; r.Amount = o.Amount; break;
                case "daily_bonus": S.Cash += o.Amount; S.Today.Bonus += o.Amount; r.Amount = o.Amount; break;
                case "extra_runner":
                {
                    int i = target >= 0 ? target : FirstTruck(x => x.Open && x.HelperUntil <= S.Minute);
                    S.Trucks[i].HelperUntil = S.Minute + Gd.Svc.HelperMin;
                    break;
                }
                case "bonus_patties":
                {
                    int i = target >= 0 ? target : FirstTruck(x => x.Load[Stock.Patty] + def.Amount <= TruckCap(x, Stock.Patty));
                    S.Trucks[i].Load[Stock.Patty] += def.Amount;
                    r.Amount = def.Amount;
                    break;
                }
                case "quick_prep": FinishBatch(); break;
                case "crisis_rescue":
                {
                    int i = target >= 0 ? target : FirstTruck(Troubled);
                    var t = S.Trucks[i];
                    if (t.Flaring) { t.FlareUntil = 0; t.Grease = 0; }
                    if (t.Propane <= 0) t.Propane = 1;
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
        public void EndTutorial() { S.Tutorial = false; EndHandTicket(); }
    }
}
