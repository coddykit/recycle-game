// Pour & Roast sim core — engine independent (no UnityEngine). C# 9 / .NET Standard 2.1.
// One career: a harbour-town coffee business that grows from a quayside cart. A service day runs 07:00-18:00 game time
// (~3.7 real minutes at 1x):
//   plan   (07:00, paused) — forecast, menu, milk order, pastries, green coffee, roasting, purchases;
//   live                   — customers order (walk-ins + pre-orders with a pickup time), tickets stream onto the rail,
//                            baristas work them at the espresso group heads and the steam wands by the player's rule;
//   report (18:00)         — open tickets close, milk spoils, wages + rent, reviews -> stars; NextDay().
// Partial class: this file = construction, queries, time, demand; Bar.cs = tickets, stations, staff, reviews;
// Roastery.cs = green market, roasting, resting, blending, wholesale; Economy.cs = day cycle, purchases, research,
// offline, rewards; Crises.cs = weather + crises.
using System;
using System.Collections.Generic;

namespace PourAndRoast.Sim
{
    public sealed class SimEvent
    {
        public string Type;     // order, balk, served, walked, ready, crisis, roast_done, blended, research_done, venue, day_done, day_start, ...
        public int A = -1, B = -1;
        public double V;
        public string Text = "";
        public override string ToString() => Type + "(" + A + "," + B + "," + V + "," + Text + ")";
    }

    public sealed partial class Simulation
    {
        public readonly GameData Gd;
        public State S;
        public readonly Rng Rng;
        readonly List<SimEvent> events = new List<SimEvent>();
        readonly Dictionary<string, bool> perks = new Dictionary<string, bool>();
        /// <summary>Emit order / balk / served events for the diorama's customers (off in balance bots).</summary>
        public bool EmitCrowd = true;
        /// <summary>The guide holds finished cups on the shelf until the player hands them over (never saved).</summary>
        public bool HoldShelf;

        public Simulation(GameData gd, long seed)
        {
            Gd = gd ?? throw new ArgumentNullException(nameof(gd));
            S = new State { Seed = seed <= 0 ? 1 : seed };
            Rng = new Rng(S.Seed);
            NewCareer();
            S.RngState = Rng.State;
        }

        /// <summary>From a decoded save (SaveCodec).</summary>
        public Simulation(GameData gd, State s)
        {
            Gd = gd ?? throw new ArgumentNullException(nameof(gd));
            S = s;
            Rng = new Rng(1) { State = s.RngState <= 0 ? 1 : s.RngState };
        }

        public JObj Snapshot()
        {
            S.RngState = Rng.State;
            return S.ToJson(Gd);
        }

        void NewCareer()
        {
            S.Day = 1;
            S.Minute = Gd.DayStart;
            S.Phase = Phases.Plan;
            S.Cash = Gd.StartCash;
            S.Rating = Gd.StartRating;
            S.Venue = Gd.StartVenue;
            S.Venues.Add(NewVenue(Gd.Venue(Gd.StartVenue)));
            foreach (var role in Gd.StartStaff) Hire(role, Stations.Any);
            S.Menu.AddRange(Gd.StartMenu);
            S.Par = new int[Gd.Menu.Count];
            S.Pastry = new int[Gd.Menu.Count];
            for (int i = 0; i < Gd.Menu.Count; i++) if (Gd.Menu[i].IsPastry) S.Par[i] = Gd.PastryParDefault;
            S.Milk = new[] { Gd.StartMilk[0], Gd.StartMilk.Length > 1 ? Gd.StartMilk[1] : 0 };
            S.MilkOrder = new[] { Gd.StartMilkOrder[0], Gd.StartMilkOrder.Length > 1 ? Gd.StartMilkOrder[1] : 0 };
            S.Supplier = Gd.StartSupplier;
            S.Ice = Gd.StartIce;
            S.BeanKg = Gd.StartBeansKg;
            S.BeanQ = Gd.StartBeansQ;
            S.Green = new double[Gd.Origins.Count];
            S.Market = new double[Gd.Origins.Count];
            for (int i = 0; i < S.Market.Length; i++) S.Market[i] = 1;
            foreach (var c in Gd.Contracts) S.Contracts.Add(new ContractState { Id = c.Id });
            for (int d = 0; d < 4; d++) S.Forecast.Add(ForecastFor(1 + d));
            S.Today = new DayStats { Day = 1, Venue = S.Venue, Weather = Weather };
        }

        VenueState NewVenue(VenueDef d) => new VenueState { Id = d.Id, Heads = d.Heads0, Wands = d.Wands0, Ice = d.Ice0, Grinder = 1, Tune = 100 };

        StaffState Hire(string role, string station)
        {
            var s = new StaffState
            {
                Id = S.NextStaffId++, Role = role, Station = station, Venue = S.Venue,
                Trait = S.Staff.Count == 0 || Gd.TraitOrder.Count == 0 ? (Gd.TraitOrder.Count > 0 ? Gd.TraitOrder[0] : "steady") : Gd.TraitOrder[Rng.RangeI(0, Gd.TraitOrder.Count - 1)],
            };
            S.Staff.Add(s);
            return s;
        }

        // ------------------------------------------------------------------------------------------------ queries

        public List<SimEvent> DrainEvents()
        {
            var o = new List<SimEvent>(events);
            events.Clear();
            return o;
        }

        void Emit(string type, int a = -1, int b = -1, double v = 0, string text = "") => events.Add(new SimEvent { Type = type, A = a, B = b, V = v, Text = text ?? "" });

        public string Phase => S.Phase;
        public bool Has(string research) => S.Research.Contains(research);
        public bool Owns(string venue) => S.Venues.Exists(v => v.Id == venue);
        public bool Perk(string id) => perks.TryGetValue(id, out bool b) && b;
        public VenueState Venue => S.Active;
        public VenueDef VenueDef => Gd.Venue(S.Venue) ?? Gd.Venues[0];
        /// <summary>The menu tier the active venue can serve (its chapter).</summary>
        public int Tier => VenueDef.Chapter;
        public int Chapter
        {
            get
            {
                int c = 1;
                foreach (var v in S.Venues) { var d = Gd.Venue(v.Id); if (d != null) c = Math.Max(c, d.Chapter); }
                return c;
            }
        }
        public bool RoasteryOpen => Owns("roastery");
        public int MaxSpeed => Perk("speed") ? Gd.PaidMaxSpeed : Gd.FreeMaxSpeed;
        public bool Weekend => (S.Day - 1) % 7 >= 5;
        public string Weather => S.Forecast.Count > 0 ? S.Forecast[0] : Weathers.Sunny;
        public WeatherDef WeatherDef => Gd.Weather.TryGetValue(Weather, out var w) ? w : new WeatherDef { Id = Weathers.Sunny };
        /// <summary>Days of the forecast strip the player sees beyond today (the forecast board research shows 3).</summary>
        public int ForecastDays => Has("forecast_board") ? 3 : 1;
        public int HourIndex => Math.Max(0, Math.Min(Gd.HourCurve.Length - 1, (int)((S.Minute - Gd.DayStart) / 60.0)));

        public bool Available(MenuItemDef m) => m != null && m.Tier <= Tier;
        public bool OnMenu(MenuItemDef m) => Available(m) && S.Menu.Contains(m.Id);
        public int MenuCount { get { int n = 0; foreach (var m in Gd.Menu) if (!m.IsPastry && OnMenu(m)) n++; return n; } }
        public int RailCap => Gd.RailBase + Gd.RailPerLevel * (Venue != null ? Venue.Rail : 0) + (Has("rail_tablet") ? 2 : 0);
        public double IceCap => Venue != null && Venue.Ice < Gd.IceCap.Length ? Gd.IceCap[Venue.Ice] : 0;
        public double IcePerHour => Venue != null && Venue.Ice < Gd.IcePerHour.Length ? Gd.IcePerHour[Venue.Ice] : 0;
        public double DrumKg => S.Drum >= 0 && S.Drum < Gd.DrumKg.Length ? Gd.DrumKg[S.Drum] : 0;
        public bool CrisesAllowed => !S.Tutorial && S.Day > Gd.EventFreeDays;

        /// <summary>Footfall multiplier from the reputation stars (2.5★ = 1.0).</summary>
        public double Footfall => Math.Max(0.3, Gd.FootfallMin + Gd.FootfallPerStar * Math.Max(0, Math.Min(5, S.Rating)));

        public double EventMult(string id)
        {
            double m = 1;
            foreach (var e in S.Events) if (e.Id == id) m *= e.Mult;
            return m;
        }

        /// <summary>Customers per hour now at the active venue (before the weather's walk-in share).</summary>
        public double ArrivalsPerHour()
        {
            var d = VenueDef;
            double seats = 1 + Gd.SeatFootfall * (Venue != null ? Venue.Seats : 0) + (Has("window_seats") ? 0.06 : 0);
            return d.PerHour * Gd.HourCurve[HourIndex] * Footfall * (Weekend ? Gd.WeekendMult : 1.0) * seats * EventMult("review_spree");
        }

        // ------------------------------------------------------------------------------------------------ time

        public void SetSpeed(int speed) => S.Speed = Math.Max(1, Math.Min(MaxSpeed, speed));
        public void SetPaused(bool paused) => S.Paused = paused;

        /// <summary>Store perks (never saved): speed, automation, roast_pass, offline_plus, gold_trim, slots.</summary>
        public void SetPerks(Dictionary<string, bool> p)
        {
            perks.Clear();
            if (p != null) foreach (var kv in p) perks[kv.Key] = kv.Value;
            if (S.Speed > MaxSpeed) S.Speed = MaxSpeed;
        }

        /// <summary>Every frame with real seconds. Research and the roaster run on real time in every phase; the day only while live.</summary>
        public void Advance(double realDt)
        {
            if (realDt <= 0 || double.IsNaN(realDt)) return;
            realDt = Math.Min(realDt, 1.0);   // a hitch never jumps the sim
            TickResearch(realDt);
            TickRoast(realDt);
            if (S.Phase != Phases.Live || S.Paused) return;
            double gm = realDt * Gd.GameMinPerRealS * Math.Max(1, S.Speed);
            while (gm > 1e-9 && S.Phase == Phases.Live)
            {
                double step = Math.Min(0.5, gm);
                Step(step);
                gm -= step;
            }
        }

        /// <summary>Advances game minutes directly (tests, guide fast-forward). Stops at the day end.</summary>
        public void AdvanceMinutes(double minutes)
        {
            while (minutes > 1e-9 && S.Phase == Phases.Live)
            {
                double step = Math.Min(0.5, minutes);
                Step(step);
                minutes -= step;
            }
        }

        void Step(double dt)
        {
            StepSupplies(dt);
            Demand(dt);
            StepBar(dt);
            S.Minute += dt;
            if (S.Minute >= Gd.DayEnd - 1e-9) EndDay();
        }

        // ------------------------------------------------------------------------------------------------ demand

        void Demand(double dt)
        {
            if (S.Tutorial) return;   // the guide places its own orders (TutorialOrder)
            double expected = ArrivalsPerHour() * dt / 60.0;
            int n = Sample(expected);
            var d = VenueDef;
            var w = WeatherDef;
            var weights = new double[Gd.Customers.Count];
            for (int c = 0; c < weights.Length; c++)
            {
                var cd = Gd.Customers[c];
                double x = c < d.Weights.Length ? d.Weights[c] : 0;
                if (S.Rating < cd.RatingMin) x = 0;
                if (Weekend) x *= cd.Weekend;
                if (cd.Id == "regular" && Has("loyalty_cards")) x *= 1.5;
                weights[c] = x;
            }
            for (int k = 0; k < n; k++)
            {
                int c = Rng.PickWeighted(weights);
                if (c < 0) continue;
                var cd = Gd.Customers[c];
                double pre = Math.Min(0.8, cd.Pre * w.Pre * (Has("pickup_app") ? 1.5 : 1.0) * (Tier <= 1 ? 0.6 : 1.0));
                bool isPre = Rng.Frand() < pre;
                if (!isPre && Rng.Frand() >= w.Walk) continue;   // the weather kept them at home
                S.Today.Customers++;
                if (!isPre && OpenTickets() >= RailCap)
                {
                    S.Today.Balked++;
                    if (EmitCrowd) Emit("balk", -1, c);
                    continue;
                }
                NewTicket(c, isPre);
            }
        }

        int Sample(double expected)
        {
            if (expected <= 0) return 0;
            int k = (int)Math.Floor(expected);
            if (Rng.Frand() < expected - k) k++;
            return k;
        }

        /// <summary>Tickets on the rail that are not finished yet (the rail's capacity counts these).</summary>
        public int OpenTickets()
        {
            int n = 0;
            foreach (var t in S.Tickets) if (!t.Ready) n++;
            return n;
        }

        TicketState NewTicket(int cust, bool pre)
        {
            var cd = Gd.Customers[cust];
            var t = new TicketState { Id = S.NextTicketId++, Cust = cust, Pre = pre, Arrived = S.Minute };
            t.Due = pre ? S.Minute + Rng.RangeI(Gd.PreLeadMin, Gd.PreLeadMax) : S.Minute + cd.Patience;
            int items = Rng.RangeI(cd.ItemsMin, cd.ItemsMax);
            bool drink = false;
            for (int i = 0; i < items; i++)
            {
                int m = -1;
                if (drink && Rng.Frand() < cd.Pastry) m = PickPastry();
                if (m < 0) { m = PickDrink(cd); drink = true; }
                if (m < 0) continue;
                AddItem(t, m, cd);
            }
            if (t.Items.Count == 0) return null;
            if (pre) S.Today.PreOrders++;
            S.Tickets.Add(t);
            if (EmitCrowd || S.Tutorial) Emit("order", t.Id, cust, pre ? 1 : 0);
            return t;
        }

        void AddItem(TicketState t, int m, CustomerDef cd)
        {
            var def = Gd.Menu[m];
            var it = new ItemState { M = m, Q = def.IsPastry ? PastryQuality : S.BeanQ };
            if (def.Milk) it.Milk = Rng.Frand() < cd.Oat ? 2 : 1;
            // the pre-brew urn serves simple hot coffee at once (forecast prep)
            if (def.Prep && S.PrepHot > 0) { S.PrepHot--; it.Stage = def.Tasks.Length; it.Q = S.BeanQ - 2; }
            t.Items.Add(it);
            S.Today.Items++;
        }

        public double PastryQuality => Has("pastry_oven") ? Gd.OvenQ : Gd.PastryQ;

        int PickPastry()
        {
            var w = new double[Gd.Menu.Count];
            bool any = false;
            for (int i = 0; i < w.Length; i++)
            {
                var m = Gd.Menu[i];
                if (!m.IsPastry || !OnMenu(m) || S.Pastry[i] <= ReservedPastry(i)) continue;
                w[i] = m.Pop; any = true;
            }
            return any ? Rng.PickWeighted(w) : -1;
        }

        int ReservedPastry(int m)
        {
            int n = 0;
            foreach (var t in S.Tickets) foreach (var it in t.Items) if (it.M == m && it.Stage == 0 && !it.Failed) n++;
            return n;
        }

        int PickDrink(CustomerDef cd)
        {
            double iced = Math.Min(0.9, cd.Iced * WeatherDef.Iced);
            bool wantIced = Rng.Frand() < iced;
            var w = new double[Gd.Menu.Count];
            bool any = false;
            for (int pass = 0; pass < 2 && !any; pass++)
            {
                for (int i = 0; i < w.Length; i++)
                {
                    var m = Gd.Menu[i];
                    w[i] = 0;
                    if (m.IsPastry || !OnMenu(m) || m.Iced != wantIced) continue;
                    w[i] = m.Pop; any = true;
                }
                if (!any) wantIced = !wantIced;
            }
            return any ? Rng.PickWeighted(w) : -1;
        }

        /// <summary>The guide's scripted orders (no random customers while the guide runs): espresso | latte | americano | preorder.</summary>
        public TicketState TutorialOrder(string kind)
        {
            int commuter = Math.Max(0, Gd.CustomerIndex("commuter"));
            int regular = Math.Max(0, Gd.CustomerIndex("regular"));
            var cd = Gd.Customers[commuter];
            var t = new TicketState { Id = S.NextTicketId++, Cust = commuter, Arrived = S.Minute, Due = S.Minute + cd.Patience * 3 };
            switch (kind)
            {
                case "espresso": t.Items.Add(new ItemState { M = Gd.MenuIndex("espresso"), Q = S.BeanQ }); break;
                case "latte": t.Items.Add(new ItemState { M = Gd.MenuIndex("latte"), Milk = 1, Q = S.BeanQ }); break;
                case "americano": t.Items.Add(new ItemState { M = Gd.MenuIndex("americano"), Q = S.BeanQ }); break;
                case "preorder":
                    t.Cust = regular; t.Pre = true; t.Due = S.Minute + 30;
                    t.Items.Add(new ItemState { M = Gd.MenuIndex("cappuccino"), Milk = 1, Q = S.BeanQ });
                    S.Today.PreOrders++;
                    break;
                default: return null;
            }
            foreach (var it in t.Items) if (it.M < 0) return null;
            S.Today.Customers++;
            S.Today.Items += t.Items.Count;
            S.Tickets.Add(t);
            Emit("order", t.Id, t.Cust, t.Pre ? 1 : 0);
            return t;
        }

        public TicketState Ticket(int id) => S.Tickets.Find(t => t.Id == id);
        public MenuItemDef ItemDef(ItemState it) => it.M >= 0 && it.M < Gd.Menu.Count ? Gd.Menu[it.M] : null;
        public bool ItemDone(ItemState it) { var d = ItemDef(it); return it.Failed || d == null || it.Stage >= d.Tasks.Length; }
        public string NextTask(ItemState it) { var d = ItemDef(it); return d == null || it.Failed || it.Stage >= d.Tasks.Length ? "" : d.Tasks[it.Stage]; }
    }
}
