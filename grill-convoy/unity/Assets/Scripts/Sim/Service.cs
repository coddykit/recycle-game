// Grill Convoy sim core — inside the truck: the layout grid, the line's throughput, the crowd and the guide's
// hand-cooked first ticket (design §4 loop 2, §5.1-5.4).
//   * The line has three stages: GRILL (plates) -> ASSEMBLY bench -> WINDOW. Each stage's rate = stations × base rate ×
//     staffing (no crew on a stage = half speed; each extra cook +30 %) × adjacency bonuses (toaster / sauce rail next to
//     the bench, drink fridge next to the window). Walking between grill, bench and window costs time: every tile of
//     distance beyond "adjacent" slows the whole line (a runner halves the walk). Throughput = the slowest stage, plus the
//     runner's float. A long menu board slows the bench (fewer cards = faster line).
//   * Customers arrive at a pitch by its crowd curve × weather × weekend × match day × pitch reputation × rival, look at the
//     menu board (appeal per customer type), queue with a patience bar, walk away when the line is too long or too slow,
//     and are turned away when the truck sold out of patties or buns. Order-ahead tickets (research) skip the walk-away.
//   * Grease builds with every patty: past the safe level a flare-up can start (grill half speed, patties burn) until the
//     crew puts it out (tap Extinguish) or it burns out. Propane drains per patty; an empty tank darkens the grill.
using System;
using System.Collections.Generic;

namespace GrillConvoy.Sim
{
    public sealed class LineStats
    {
        public double Grill, Assembly, Window, Walk = 1, Throughput;
        public int WalkTiles, Runners;
        public string Bottleneck = "";
        public bool Complete;
        public double SideChance;   // fraction of a customer's side wish the truck can serve (fryer / drink fridge)
    }

    public sealed partial class Simulation
    {
        // ------------------------------------------------------------------------------------------------ layout queries

        public int GridW(TruckState t) => t.Extended ? VehicleOf(t).WExt : VehicleOf(t).W;
        public int GridH(TruckState t) => t.Extended ? VehicleOf(t).HExt : VehicleOf(t).H;

        public int TileIndexAt(TruckState t, int x, int y)
        {
            for (int i = 0; i < t.Layout.Count; i++) if (t.Layout[i].X == x && t.Layout[i].Y == y) return i;
            return -1;
        }

        public int CountStations(TruckState t, string type)
        {
            int n = 0;
            foreach (var s in t.Layout) if (s.Type == type) n++;
            return n;
        }

        /// <summary>Smallest Manhattan distance between a tile of type a and a tile of type b (99 = one is missing).</summary>
        public int Dist(TruckState t, string a, string b)
        {
            int best = 99;
            foreach (var p in t.Layout)
            {
                if (p.Type != a) continue;
                foreach (var q in t.Layout)
                {
                    if (q.Type != b || ReferenceEquals(p, q)) continue;
                    best = Math.Min(best, Math.Abs(p.X - q.X) + Math.Abs(p.Y - q.Y));
                }
            }
            return best;
        }

        public bool Adjacent(TruckState t, string a, string b) => Dist(t, a, b) == 1;

        int CrewCount(TruckState t, string role)
        {
            int n = 0;
            foreach (var c in t.Crew) if (c == role) n++;
            return n;
        }

        double Staff(int n) => (n <= 0 ? Gd.Svc.Unstaffed : 1 + Gd.Svc.ExtraStaff * (n - 1)) * (Has("crew_training") ? 1.1 : 1.0);

        double StationRate(string type) => Gd.StationDefs.TryGetValue(type, out var d) ? d.Rate : 0;

        /// <summary>The line's numbers for this truck's current layout / crew / menu (burgers per game minute). Pure.</summary>
        public LineStats Line(TruckState t)
        {
            var L = new LineStats();
            var sv = Gd.Svc;
            int g = CountStations(t, Stations.Grill), a = CountStations(t, Stations.Assembly), w = CountStations(t, Stations.Window);
            L.Complete = g > 0 && a > 0 && w > 0;
            L.Runners = CrewCount(t, Roles.Runner) + (t.HelperUntil > S.Minute ? 1 : 0);
            L.Grill = g * StationRate(Stations.Grill) * Staff(CrewCount(t, Roles.Griller)) * (Has("grill_plus") ? 1.15 : 1.0);
            L.Assembly = a * StationRate(Stations.Assembly) * Staff(CrewCount(t, Roles.Assembler))
                * (Adjacent(t, Stations.Toaster, Stations.Assembly) ? sv.ToasterAdj : 1.0)
                * (Adjacent(t, Stations.Sauce, Stations.Assembly) ? sv.SauceAdj : 1.0)
                * Math.Max(0.6, 1.0 - sv.MenuPenalty * Math.Max(0, t.Menu.Count - 2));
            L.Window = w * StationRate(Stations.Window) * Staff(CrewCount(t, Roles.Cashier))
                * (Adjacent(t, Stations.Fridge, Stations.Window) ? sv.FridgeAdj : 1.0);
            int walk = 0;
            if (L.Complete)
            {
                walk = Math.Max(0, Dist(t, Stations.Grill, Stations.Assembly) - 1) + Math.Max(0, Dist(t, Stations.Assembly, Stations.Window) - 1);
                if (L.Runners > 0) walk /= 2;
            }
            L.WalkTiles = walk;
            L.Walk = 1.0 / (1.0 + sv.WalkPerTile * walk);
            // the runner floats to whichever stage is the slowest
            double gg = L.Grill, aa = L.Assembly, ww = L.Window;
            for (int r = 0; r < L.Runners; r++)
            {
                if (gg <= aa && gg <= ww) gg += sv.RunnerBoost;
                else if (aa <= ww) aa += sv.RunnerBoost;
                else ww += sv.RunnerBoost;
            }
            double min = Math.Min(gg, Math.Min(aa, ww));
            L.Bottleneck = gg <= aa && gg <= ww ? Stations.Grill : aa <= ww ? Stations.Assembly : Stations.Window;
            L.Throughput = L.Complete && t.Crew.Count > 0 ? min * L.Walk : 0;
            bool fryer = CountStations(t, Stations.Fryer) > 0, fridge = CountStations(t, Stations.Fridge) > 0;
            L.SideChance = fryer && fridge ? 1.0 : fryer ? 0.65 : fridge ? 0.55 : 0;
            return L;
        }

        /// <summary>The throughput right now (flare-up, empty propane, clean cycle, all hands).</summary>
        public double LiveThroughput(TruckState t)
        {
            var L = Line(t);
            double x = L.Throughput;
            if (x <= 0) return 0;
            bool grillOff = t.Propane <= 0 || t.GrillOffUntil > S.Minute;
            if (grillOff) return 0;
            if (t.Flaring) x *= 0.5;
            if (t.AllHandsUntil > S.Minute) x *= Gd.Svc.AllHandsMult;
            return x;
        }

        public int QueueCap(TruckState t) => Gd.Svc.QueueCap + (CountStations(t, Stations.Window) > 1 ? Gd.Svc.QueueCapWindow : 0);

        // ------------------------------------------------------------------------------------------------ demand

        double RepMult(int node) => 0.85 + 0.003 * PitchState(node).Rep;

        /// <summary>Crowd multiplier of a pitch now (match day window, halftime stampede).</summary>
        public double EventMult(int node)
        {
            var n = NodeAt(node);
            double m = 1;
            if (n.Match && MatchDay(S.Day) && S.Minute >= Gd.MatchFrom && S.Minute < Gd.MatchTo)
            {
                m *= Gd.MatchMult;
                if (S.Minute >= Gd.HalftimeFrom && S.Minute < Gd.HalftimeTo) m *= Gd.HalftimeMult / Gd.MatchMult;
            }
            return m;
        }

        public bool Halftime(int node) => NodeAt(node).Match && MatchDay(S.Day) && S.Minute >= Gd.HalftimeFrom && S.Minute < Gd.HalftimeTo;

        public double RivalMult(int node, TruckState t)
        {
            foreach (var e in S.Events)
            {
                if (e.Id != "rival" || e.Target != node) continue;
                var d = Gd.Event("rival");
                if (t != null && t.Promo) return d != null ? d.PromoMult : 0.92;
                return Has("rival_scout") && d != null ? d.ScoutMult : e.Mult;
            }
            return 1.0;
        }

        /// <summary>Passers-by per hour at a pitch now (before the menu board and the line decide who stops).</summary>
        public double PitchRate(int node, double minute = -1)
        {
            var n = NodeAt(node);
            if (!n.IsPitch) return 0;
            var c = Gd.Curve(n.Curve);
            int h = minute < 0 ? HourIndex : Math.Max(0, Math.Min(c.Length - 1, (int)((minute - Gd.DayStart) / 60.0)));
            double r = n.Rate * c[Math.Min(h, c.Length - 1)] * WeatherMult(node) * RepMult(node);
            if (Weekend) r *= n.Weekend;
            if (S.Day <= Gd.EventFreeDays) r *= Gd.Svc.FirstDaysDemand;
            return r;
        }

        /// <summary>Expected customers at a pitch over a whole day (forecast card, pack suggestion).</summary>
        public double ForecastCustomers(int node, bool tomorrow = false)
        {
            var n = NodeAt(node);
            if (!n.IsPitch) return 0;
            string w = tomorrow ? S.Forecast : S.Weather;
            int day = tomorrow ? S.Day + 1 : S.Day;
            var c = Gd.Curve(n.Curve);
            double sum = 0;
            foreach (var x in c) sum += x;
            double r = n.Rate * sum * n.WeatherMult(w) * RepMult(node);
            if (WeekendDay(day)) r *= n.Weekend;
            if (day <= Gd.EventFreeDays) r *= Gd.Svc.FirstDaysDemand;
            if (n.Match && MatchDay(day)) r += n.Rate * (Gd.MatchMult - 1) * (Gd.MatchTo - Gd.MatchFrom) / 60.0 * 0.6 + n.Rate * (Gd.HalftimeMult - Gd.MatchMult) * (Gd.HalftimeTo - Gd.HalftimeFrom) / 60.0 * 0.6;
            return r;
        }

        double Appeal(CardDef c, int arch)
        {
            double a = arch >= 0 && arch < c.Appeal.Length ? c.Appeal[arch] : 1.0;
            if (c.Until > 0) a *= S.Minute < c.Until ? 1.0 : 0.3;
            return a;
        }

        public int AverageItems(int node)
        {
            var n = NodeAt(node);
            double items = 0, tw = 0;
            for (int a = 0; a < Gd.Archetypes.Count && a < n.Arch.Length; a++) { items += n.Arch[a] * Gd.Archetypes[a].Mean; tw += n.Arch[a]; }
            return (int)Math.Round(tw > 0 ? items / tw * 100 : 100);
        }

        // ------------------------------------------------------------------------------------------------ the line

        void StepService(double dt)
        {
            for (int ti = 0; ti < S.Trucks.Count; ti++)
            {
                var t = S.Trucks[ti];
                if (!t.Open) continue;
                if (t.FlareUntil > 0 && S.Minute >= t.FlareUntil) { t.FlareUntil = 0; Emit("flare_out", ti); }
                t.OpenMin += dt;
                S.Today.OpenMin += dt;
                bool soldOut = t.Load[Stock.Patty] <= 0 || t.Load[Stock.Bun] <= 0;
                if (soldOut) { t.SelloutMin += dt; S.Today.SelloutMin += dt; }
                if (ti == 0 && S.HandStage > 0) { StepHand(dt); continue; }   // the guide's first ticket is cooked by hand
                Arrivals(ti, t, dt, soldOut);
                Serve(ti, t, dt);
                Patience(ti, t);
                if (t.Flaring && Rng.Frand() < Gd.Svc.FlareWastePerMin * dt && t.Load[Stock.Patty] > 0)
                {
                    t.Load[Stock.Patty]--;
                    S.Today.Waste++;
                    S.Today.WasteValue += PattyCost;
                }
                if (t.Auto) HeadCook(ti, t);
            }
        }

        double PattyCost => Gd.StockDefs[Stock.Patty].Cost / Math.Max(1, Gd.StockDefs[Stock.Patty].Batch);

        void Arrivals(int ti, TruckState t, double dt, bool soldOut)
        {
            int node = t.AtNode;
            var n = NodeAt(node);
            if (!n.IsPitch) return;
            double rate = PitchRate(node) * EventMult(node) * RivalMult(node, t) / Math.Max(1, TrucksAt(node));
            int k = Sample(rate * dt / 60.0);
            if (k <= 0) return;
            bool halftime = Halftime(node);
            double sideChance = Line(t).SideChance;
            bool fridge = CountStations(t, Stations.Fridge) > 0;
            for (int i = 0; i < k; i++)
            {
                int a = Rng.PickWeighted(n.Arch);
                if (a < 0) a = 0;
                var ar = Gd.Archetypes[a];
                // the menu board decides who stops
                var w = new double[t.Menu.Count];
                double best = 0;
                for (int c = 0; c < t.Menu.Count; c++)
                {
                    var cd = Gd.Card(t.Menu[c]);
                    w[c] = cd != null ? Appeal(cd, a) : 0;
                    best = Math.Max(best, w[c]);
                }
                double conv = Math.Min(1.0, Gd.Svc.ConvBase + Gd.Svc.ConvAppeal * best + Gd.Svc.ConvCard * Math.Min(4, t.Menu.Count)) * Gd.TierAppeal[t.Tier];
                if (Rng.Frand() >= conv) continue;
                t.Arrivals++;
                S.Today.Arrivals++;
                if (soldOut)
                {
                    t.SoldOut++;
                    S.Today.SoldOut++;
                    if (EmitCrowd) Emit("sold_out", ti, a);
                    continue;
                }
                bool ahead = Has("order_ahead") && t.Queue.Count >= 4 && Rng.Frand() < Gd.Svc.OrderAheadShare * (S.Weather == "rain" ? 2 : 1);
                if (!ahead && t.Queue.Count >= QueueCap(t))
                {
                    t.Walked++;
                    S.Today.Walked++;
                    if (EmitCrowd) Emit("walked", ti, a, 1, "line");
                    continue;
                }
                int card = Rng.PickWeighted(w);
                if (card < 0) card = 0;
                double sideWish = ar.Side * (S.Weather == "hot" && fridge ? 1.6 : 1.0);
                t.Queue.Add(new Customer
                {
                    Arch = a, Card = Math.Max(0, Gd.CardIndex(t.Menu[card])), Items = Rng.RangeI(ar.Min, ar.Max), Side = Rng.Frand() < Math.Min(1, sideWish) * sideChance,
                    Ahead = ahead, At = S.Minute, Patience = ar.Patience * (halftime ? Gd.MatchPatience : 1.0) * (ahead ? Gd.Svc.OrderAheadPatience : 1.0),
                });
                if (EmitCrowd) Emit("joined", ti, a);
            }
        }

        double OrderWork(TruckState t, Customer c)
        {
            var cd = Gd.Cards[Math.Max(0, Math.Min(Gd.Cards.Count - 1, c.Card))];
            double w = c.Items * cd.Work + (c.Side ? Gd.Svc.SideWork * (Adjacent(t, Stations.Fryer, Stations.Window) ? 0.8 : 1.0) : 0);
            if (c.Ahead && Adjacent(t, Stations.Wrap, Stations.Window)) w /= Gd.Svc.WrapAdj;
            return w;
        }

        void Serve(int ti, TruckState t, double dt)
        {
            double rate = LiveThroughput(t);
            if (t.Queue.Count == 0) { t.Work = Math.Min(t.Work, 1.0); return; }
            t.Work += rate * dt;
            int guard = 0;
            while (t.Queue.Count > 0 && guard++ < 30)
            {
                int head = t.Queue.FindIndex(c => c.Ahead);
                if (head < 0) head = 0;
                var c = t.Queue[head];
                double need = OrderWork(t, c);
                if (t.Work < need) break;
                t.Work -= need;
                t.Queue.RemoveAt(head);
                var cd = Gd.Cards[Math.Max(0, Math.Min(Gd.Cards.Count - 1, c.Card))];
                int patties = c.Items * cd.Patties;
                if (t.Load[Stock.Patty] < patties || t.Load[Stock.Bun] < c.Items)
                {
                    t.SoldOut++;
                    S.Today.SoldOut++;
                    if (EmitCrowd) Emit("sold_out", ti, c.Arch);
                    continue;
                }
                t.Load[Stock.Patty] -= patties;
                t.Load[Stock.Bun] -= c.Items;
                double revenue = c.Items * cd.Price * (t.Promo ? 0.9 : 1.0);
                bool side = c.Side && t.Load[Stock.Side] > 0;
                if (side)
                {
                    t.Load[Stock.Side]--;
                    revenue += Gd.SidePrice * (Adjacent(t, Stations.Fryer, Stations.Window) ? Gd.Svc.FryerAdj : 1.0);
                    S.Today.Sides++;
                }
                double fix = c.Items * Gd.TierCost[t.Tier];
                S.Cash += revenue - fix;
                S.Today.Sales += revenue;
                S.Today.Fixings += fix;
                t.Sales += revenue;
                double wait = Math.Max(0, S.Minute - c.At);
                t.WaitSum += wait;
                S.Today.WaitSum += wait;
                t.Served++;
                t.Items += c.Items;
                S.Today.Served++;
                S.Today.Items += c.Items;
                S.Burgers += c.Items;
                PitchState(t.AtNode).Served++;
                Cook(ti, t, patties);
                Emit("served", ti, c.Items, revenue);
            }
        }

        /// <summary>Grease and propane per patty; a dirty grill can flare up (never during the guide / the first days).</summary>
        void Cook(int ti, TruckState t, int patties)
        {
            t.Grease += patties;
            double before = t.Propane;
            t.Propane = Math.Max(0, t.Propane - patties * Gd.Svc.PropanePerPatty * (Has("propane_gauge") ? 0.85 : 1.0));
            if (before > Gd.Svc.PropaneLow && t.Propane <= Gd.Svc.PropaneLow) Emit("propane_low", ti);
            if (t.Propane <= 0 && before > 0) Emit("crisis", ti, -1, 0, "propane");
            if (!t.Flaring && CrisesAllowed && t.Grease > Gd.Svc.GreaseSafe)
            {
                double p = (t.Grease - Gd.Svc.GreaseSafe) * Gd.Svc.FlarePerPatty * patties * (Has("fire_drill") ? 0.5 : 1.0);
                if (Rng.Frand() < p) Flare(ti);
            }
        }

        void Patience(int ti, TruckState t)
        {
            for (int i = t.Queue.Count - 1; i >= 0; i--)
            {
                var c = t.Queue[i];
                if (S.Minute - c.At <= c.Patience) continue;
                t.Queue.RemoveAt(i);
                t.Walked++;
                S.Today.Walked++;
                if (EmitCrowd) Emit("walked", ti, c.Arch, 1, "wait");
            }
        }

        /// <summary>Head cook rules (Dispatcher research, rule slot): clean a greasy grill in a quiet moment, swap an empty
        /// propane bottle, put out a flare-up, call the restock run before the truck sells out.</summary>
        void HeadCook(int ti, TruckState t)
        {
            if (t.Flaring) Extinguish(ti);
            if (t.Propane <= 0) SwapPropane(ti);
            if (t.Grease > Gd.Svc.GreaseSafe && t.Queue.Count <= 1 && t.GrillOffUntil <= S.Minute) Clean(ti);
            int perOrder = Math.Max(1, AverageItems(t.AtNode) / 100);
            if (t.Load[Stock.Patty] < 12 * perOrder && S.Commissary.Stock[Stock.Patty] > 30 && S.Minute < Gd.DayEnd - 120) Restock(ti);
        }

        // ------------------------------------------------------------------------------------------------ player verbs (live)

        public bool CrisesAllowed => !S.Tutorial && S.Day > Gd.EventFreeDays;

        public void Flare(int ti)
        {
            var t = S.Trucks[ti];
            if (t.Flaring || !t.Open) return;
            t.FlareUntil = S.Minute + Gd.Svc.FlareMin;
            Emit("crisis", ti, -1, 0, "flare");
        }

        public string Extinguish(int ti)
        {
            if (ti < 0 || ti >= S.Trucks.Count) return "invalid";
            var t = S.Trucks[ti];
            if (!t.Flaring) return "nothing";
            t.FlareUntil = 0;
            t.Grease = Math.Min(t.Grease, Gd.Svc.GreaseSafe * 0.5);
            Emit("extinguished", ti);
            return "";
        }

        public string Clean(int ti)
        {
            if (ti < 0 || ti >= S.Trucks.Count) return "invalid";
            var t = S.Trucks[ti];
            if (t.Grease < 1) return "nothing";
            t.Grease = 0;
            if (t.Open && S.Phase == Phases.Live) t.GrillOffUntil = S.Minute + Gd.Svc.CleanMin;
            Emit("cleaned", ti);
            return "";
        }

        public string SwapPropane(int ti)
        {
            if (ti < 0 || ti >= S.Trucks.Count) return "invalid";
            var t = S.Trucks[ti];
            if (t.Propane > Gd.Svc.PropaneLow) return "nothing";
            if (S.PropaneKits > 0) S.PropaneKits--;
            else
            {
                if (S.Cash < Gd.Svc.PropaneSwap) return "cash";
                S.Cash -= Gd.Svc.PropaneSwap;
                S.Today.Propane += Gd.Svc.PropaneSwap;
            }
            t.Propane = 1;
            Emit("propane_swapped", ti);
            return "";
        }

        public string AllHands(int ti)
        {
            if (ti < 0 || ti >= S.Trucks.Count) return "invalid";
            var t = S.Trucks[ti];
            if (!t.Open || S.Phase != Phases.Live) return "closed";
            if (t.AllHandsReady > S.Minute) return "cooldown";
            t.AllHandsUntil = S.Minute + Gd.Svc.AllHandsMin;
            t.AllHandsReady = S.Minute + Gd.Svc.AllHandsCooldown;
            Emit("all_hands", ti);
            return "";
        }

        public string Promo(int ti)
        {
            if (ti < 0 || ti >= S.Trucks.Count) return "invalid";
            var t = S.Trucks[ti];
            if (t.Promo) return "already";
            int node = t.Open ? t.AtNode : t.Pitch;
            if (RivalMult(node, null) >= 1.0) return "nothing";
            t.Promo = true;
            return "";
        }

        // ------------------------------------------------------------------------------------------------ the guide's hand-cooked ticket

        /// <summary>1 ticket at the window, 2 on the grill rail, 3 patty cooking, 4 ready to flip, 5 flipped, 6 assembled.</summary>
        public int HandStage => S.HandStage;

        void StepHand(double dt)
        {
            if (S.HandStage == 3)
            {
                S.HandTimer -= dt;
                if (S.HandTimer <= 0) { S.HandStage = 4; Emit("hand", 0, 4); }
            }
        }

        /// <summary>Guide verbs: ticket | grill | flip | assemble | serve. "" = ok.</summary>
        public string HandCook(string verb)
        {
            var t = S.Trucks.Count > 0 ? S.Trucks[0] : null;
            if (t == null || !t.Open || S.Phase != Phases.Live) return "closed";
            switch (verb)
            {
                case "ticket": if (S.HandStage != 1) return "order"; S.HandStage = 2; break;
                case "grill": if (S.HandStage != 2) return "order"; S.HandStage = 3; S.HandTimer = 2; break;
                case "flip": if (S.HandStage != 4) return "order"; S.HandStage = 5; break;
                case "assemble": if (S.HandStage != 5) return "order"; S.HandStage = 6; break;
                case "serve":
                {
                    if (S.HandStage != 6) return "order";
                    S.HandStage = 0;
                    var cd = Gd.Card(t.Menu.Count > 0 ? t.Menu[0] : "classic") ?? Gd.Cards[0];
                    if (t.Load[Stock.Patty] > 0) t.Load[Stock.Patty]--;
                    if (t.Load[Stock.Bun] > 0) t.Load[Stock.Bun]--;
                    S.Cash += cd.Price;
                    S.Today.Sales += cd.Price;
                    S.Today.Served++;
                    S.Today.Arrivals++;
                    S.Today.Items++;
                    S.Burgers++;
                    t.Served++;
                    t.Arrivals++;
                    t.Items++;
                    t.Sales += cd.Price;
                    t.Queue.Clear();
                    Emit("served", 0, 1, cd.Price, "hand");
                    break;
                }
                default: return "invalid";
            }
            Emit("hand", 0, S.HandStage);
            return "";
        }

        /// <summary>The guide starts the hand-cooked ticket: one office worker at the window.</summary>
        public void BeginHandTicket()
        {
            if (S.Trucks.Count == 0) return;
            S.HandStage = 1;
            var t = S.Trucks[0];
            t.Queue.Clear();
            t.Queue.Add(new Customer { Arch = 0, Card = Math.Max(0, Gd.CardIndex(t.Menu.Count > 0 ? t.Menu[0] : "classic")), Items = 1, At = S.Minute, Patience = 999 });
            Emit("joined", 0, 0);
        }

        public void EndHandTicket() { S.HandStage = 0; if (S.Trucks.Count > 0 && S.Trucks[0].Queue.Count > 0 && S.Trucks[0].Queue[0].Patience >= 999) S.Trucks[0].Queue.RemoveAt(0); }
    }
}
