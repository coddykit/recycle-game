// Pour & Roast sim core — save codec (fleet §14). Encode = canonical JSON of the whole career (a live day included:
// tickets mid-shot, a batched pair, a jammed grinder, a batch in the drum, the report screen, the guide). Decode never
// throws and never touches a running game; it accepts every state the sim can produce (negative cash after a bad day,
// empty milk, a late van, a ticket with a failed item) and refuses only what the sim can never write (unknown venues /
// roles / menu items / origins, indices out of range, another game's file).
// Keeping unreadable files (.unreadable copies, .bak fallback) is CoddyKit.SaveRepository's job.
using System;
using System.Collections.Generic;

namespace PourAndRoast.Sim
{
    public sealed class DecodeResult
    {
        public bool Ok;
        public string Error = "";
        public Simulation Sim;
    }

    public static class SaveCodec
    {
        public static string Encode(Simulation sim) => Json.Write(sim.Snapshot());

        public static DecodeResult Decode(string text, GameData gd)
        {
            if (gd == null || !gd.Ok) return Fail("no_data");
            object raw = Json.TryParse(text);
            if (!(raw is JObj o)) return Fail("invalid_json");
            try { return Decode(o, gd); }
            catch (Exception e) { return Fail("decode_exception_" + e.GetType().Name); }
        }

        static DecodeResult Fail(string e) => new DecodeResult { Error = e };

        static DecodeResult Decode(JObj o, GameData gd)
        {
            if (J.S(o, "game") != "pour-and-roast") return Fail("wrong_game");
            int v = J.I(o, "v", -1);
            if (v < 1 || v > State.Version) return Fail("version");
            var s = new State
            {
                Seed = J.L(o, "seed", 1), RngState = J.L(o, "rng", 1), Day = J.I(o, "day", 1), Minute = J.D(o, "minute", gd.DayStart),
                Phase = J.S(o, "phase", Phases.Plan), Speed = J.I(o, "speed", 1), Paused = J.B(o, "paused"), Tutorial = J.B(o, "tutorial"),
                Cash = J.D(o, "cash"), Rating = J.D(o, "rating", 2.5), Venue = J.S(o, "venue", gd.StartVenue),
                NextStaffId = J.I(o, "next_staff"), NextTicketId = J.I(o, "next_ticket", 1), Rule = J.S(o, "rule", Rules.Fifo), Batch = J.B(o, "batch"),
                MilkArriveAt = J.D(o, "milk_at", -1), RunArriveAt = J.D(o, "run_at", -1), Supplier = J.S(o, "supplier", gd.StartSupplier),
                Ice = J.D(o, "ice"), PrepHot = J.I(o, "prep_hot"), AutoBeans = J.B(o, "auto_beans", true), BeanKg = J.D(o, "bean_kg"), BeanQ = J.D(o, "bean_q", 52),
                Drum = J.I(o, "drum", 1), LowStreak = J.I(o, "low_streak"), Researching = J.S(o, "researching"), ResearchLeftS = J.D(o, "research_left"),
                OfflinePending = J.D(o, "offline_pending"), HelperUntil = J.D(o, "helper_until", -1), AutoTuneUntil = J.D(o, "autotune_until", -1),
                LastSeen = J.L(o, "last_seen"), BestDayStars = J.I(o, "best_stars"), FiveStarDays = J.I(o, "five_star_days"), Served = J.L(o, "served"),
            };
            foreach (var d in new[] { s.Minute, s.Cash, s.Rating, s.ResearchLeftS, s.OfflinePending, s.Ice, s.BeanKg, s.BeanQ, s.MilkArriveAt, s.RunArriveAt, s.HelperUntil, s.AutoTuneUntil })
                if (!Finite(d)) return Fail("not_finite");
            if (s.Day < 1 || s.Day > 1000000) return Fail("day");
            if (!Phases.Valid(s.Phase)) return Fail("phase");
            if (s.Minute < gd.DayStart - 1 || s.Minute > gd.DayEnd + 1) return Fail("minute");
            if (s.Speed < 1 || s.Speed > 8 || s.NextStaffId < 0 || s.NextTicketId < 0) return Fail("counts");
            if (!Rules.Valid(s.Rule)) return Fail("rule");
            if (!gd.Suppliers.ContainsKey(s.Supplier)) return Fail("supplier");
            if (s.Drum < 0 || s.Drum >= gd.DrumKg.Length) return Fail("drum");
            if (s.Rating < 0 || s.Rating > 5) return Fail("rating");

            foreach (var x in J.Arr(o, "venues") ?? new List<object>())
            {
                var vs = VenueState.Read(x);
                var vd = gd.Venue(vs.Id);
                if (vd == null || s.Venues.Exists(q => q.Id == vs.Id)) return Fail("venue");
                if (vs.Heads < 0 || vs.Wands < 0 || vs.Grinder < 0 || vs.Ice < 0 || vs.Rail < 0 || vs.Seats < 0 || vs.Decor < 0) return Fail("venue_levels");
                if (!Finite(vs.Tune) || !Finite(vs.Wear) || !Finite(vs.JamUntil) || !Finite(vs.BestNet)) return Fail("not_finite");
                s.Venues.Add(vs);
            }
            if (s.Venues.Count == 0 || s.VenueOf(s.Venue) == null) return Fail("venue");
            foreach (var x in J.Arr(o, "staff") ?? new List<object>())
            {
                var st = StaffState.Read(x);
                if (!gd.Roles.ContainsKey(st.Role) || !gd.Traits.ContainsKey(st.Trait) || gd.Venue(st.Venue) == null) return Fail("staff");
                if (st.Id < 0 || st.Id >= Math.Max(1, s.NextStaffId)) return Fail("staff_id");
                s.Staff.Add(st);
            }
            foreach (var x in J.Arr(o, "tickets") ?? new List<object>())
            {
                var t = TicketState.Read(x);
                if (t.Cust < 0 || t.Cust >= gd.Customers.Count) return Fail("ticket_customer");
                if (!Finite(t.Arrived) || !Finite(t.Due) || !Finite(t.ReadyAt)) return Fail("not_finite");
                foreach (var it in t.Items)
                {
                    if (it.M < 0 || it.M >= gd.Menu.Count) return Fail("ticket_item");
                    if (it.Stage < 0 || it.Stage > gd.Menu[it.M].Tasks.Length || it.Milk < 0 || it.Milk > 2) return Fail("ticket_item");
                    if (it.Worker < -2 || !Finite(it.Left) || !Finite(it.Q)) return Fail("ticket_item");
                }
                s.Tickets.Add(t);
            }
            s.Menu = J.StrList(o, "menu");
            foreach (var id in s.Menu) if (gd.MenuIndex(id) < 0) return Fail("menu");
            s.Par = new int[gd.Menu.Count];
            s.Pastry = new int[gd.Menu.Count];
            var par = J.Obj(o, "par"); var pastry = J.Obj(o, "pastry");
            for (int i = 0; i < gd.Menu.Count; i++)
            {
                if (!gd.Menu[i].IsPastry) continue;
                s.Par[i] = Math.Max(0, J.I(par, gd.Menu[i].Id, gd.PastryParDefault));
                s.Pastry[i] = Math.Max(0, J.I(pastry, gd.Menu[i].Id));
            }
            s.Milk = JW.Dbl(o, "milk", 2); s.MilkOrder = JW.Dbl(o, "milk_order", 2); s.MilkPending = JW.Dbl(o, "milk_pending", 2); s.RunPending = JW.Dbl(o, "run_pending", 2);
            for (int k = 0; k < 2; k++) { s.Milk[k] = Math.Max(0, s.Milk[k]); s.MilkOrder[k] = Math.Max(0, s.MilkOrder[k]); }
            s.Green = new double[gd.Origins.Count];
            s.Market = new double[gd.Origins.Count];
            var green = J.Obj(o, "green"); var market = J.Obj(o, "market");
            for (int i = 0; i < gd.Origins.Count; i++)
            {
                s.Green[i] = Math.Max(0, JW.D(green, gd.Origins[i].Id));
                double m = JW.D(market, gd.Origins[i].Id, 1);
                s.Market[i] = m > 0 ? m : 1;
            }
            if (J.Obj(o, "roast") is JObj ro)
            {
                s.Roast = BatchState.Read(ro);
                if (gd.OriginIndex(s.Roast.Origin) < 0) return Fail("roast");
            }
            foreach (var x in J.Arr(o, "resting") ?? new List<object>())
            {
                var b = BatchState.Read(x);
                if (gd.OriginIndex(b.Origin) < 0) return Fail("resting");
                s.Resting.Add(b);
            }
            foreach (var c in gd.Contracts)
            {
                ContractState cs = null;
                foreach (var x in J.Arr(o, "contracts") ?? new List<object>()) if (J.S(x, "id") == c.Id) cs = ContractState.Read(x);
                s.Contracts.Add(cs ?? new ContractState { Id = c.Id });
            }
            foreach (var x in J.Arr(o, "contracts") ?? new List<object>()) if (gd.Contract(J.S(x, "id")) == null) return Fail("contract");
            s.Forecast = J.StrList(o, "forecast");
            foreach (var f in s.Forecast) if (!gd.Weather.ContainsKey(f)) return Fail("forecast");
            foreach (var x in J.Arr(o, "events") ?? new List<object>()) s.Events.Add(ActiveEvent.Read(x));
            foreach (var x in J.Arr(o, "reviews") ?? new List<object>()) s.Reviews.Add(ReviewCard.Read(x));
            s.Today = DayStats.Read(J.Obj(o, "today"));
            foreach (var h in J.Arr(o, "history") ?? new List<object>()) s.History.Add(DayStats.Read(h));
            s.Research = J.StrList(o, "research");
            foreach (var r in s.Research) if (!gd.ResearchById.ContainsKey(r)) return Fail("research");
            if (s.Researching != "" && !gd.ResearchById.ContainsKey(s.Researching)) return Fail("research");
            if (J.Obj(o, "rewards") is JObj rw) foreach (var kv in rw) s.Rewards[kv.Key] = RewardUse.Read(kv.Value);
            s.Seen = J.StrList(o, "seen");
            return new DecodeResult { Ok = true, Sim = new Simulation(gd, s) };
        }

        static bool Finite(double d) => !double.IsNaN(d) && !double.IsInfinity(d);
    }
}
