// Volt City sim core — engine independent (no UnityEngine). C# 9 / .NET Standard 2.1.
using System;
using System.Collections.Generic;

namespace VoltCity.Sim
{
    public sealed class QueueResult
    {
        public bool Ok;
        public string Reason = "";
        /// <summary>Sim step the command is applied at (the next fixed step).</summary>
        public int ApplyStep;
        /// <summary>Seconds (sim time) until a ramp tap changes the plant's setpoint (UI: "takes effect in 6 s").</summary>
        public double EffectInS;
    }

    /// <summary>
    /// Volt City career facade: S is the whole save. The scene layer calls Advance(realDt) every frame, Queue(cmd) on taps,
    /// StartShift / Collect / Buy / Hookup between shifts, Offline(now) on resume, DrainEvents() to animate.
    /// Time: a live shift runs at Speed x 1 sim second per real second in fixed 0.25 s steps (integer microseconds).
    /// </summary>
    public sealed class Simulation
    {
        public readonly GameData Gd;
        public CareerState S;
        public Grid Grid;
        readonly List<SimEvent> events = new List<SimEvent>();
        Dictionary<string, bool> perks = new Dictionary<string, bool>();

        public Simulation(GameData gd, long seed = 1)
        {
            Gd = gd;
            S = new CareerState
            {
                Seed = Rng.PosMod(seed, 2147483646) + 1, Cash = gd.StartCash, RatingX100 = gd.StartRatingX100
            };
            foreach (var d in gd.StartDistricts) S.Hooked[d] = 0;
            foreach (var p in GameData.Plants) S.Plants[p] = gd.StartPlants.TryGetValue(p, out int lv) ? lv : 0;
            S.Carry.Steps["steam"] = gd.StartSteamSteps;
            S.Carry.BatMode = Math.Max(0, Array.IndexOf(Grid.Modes, gd.StartBatteryMode));
        }

        public string Phase => S.Phase;

        /// <summary>The guide's shift flag (DESIGN section 9): no losses while the coach runs.</summary>
        public bool TutorialSafe
        {
            get => S.TutorialSafe;
            set { S.TutorialSafe = value; if (Grid != null) Grid.TutorialSafe = value; }
        }

        // ------------------------------------------------------------------------------------------- commerce hook

        /// <summary>Store perks re-applied by Commerce after every load (not saved): premium, offline_plus, speed4, dispatcher, forecast_plus.</summary>
        public void SetCommercePerks(IDictionary<string, bool> p)
        {
            perks = new Dictionary<string, bool>();
            if (p != null) foreach (var kv in p) perks[kv.Key] = kv.Value;
            if (S.Speed == Gd.Speed4 && !Perk("speed4")) S.Speed = 1;
        }

        public bool Perk(string id) => perks.TryGetValue(id, out bool v) && v;

        // ------------------------------------------------------------------------------------------- shift lifecycle

        public ShiftContext Ctx()
        {
            var c = new ShiftContext { Crews = S.Crews, CrewSpeed = S.CrewSpeed, Dr = S.Dr, Carry = S.Carry.Copy() };
            foreach (var kv in S.Hooked) c.HookedGrowthPm[kv.Key] = Math.Min(Gd.GrowthMaxPm, kv.Value * Gd.GrowthPerShiftPm);
            foreach (var kv in S.Plants) c.Plants[kv.Key] = kv.Value;
            foreach (var kv in S.Lines) c.Lines[kv.Key] = kv.Value;
            c.Ties.AddRange(S.Ties);
            return c;
        }

        public PlanContext PlanCtx(int ahead = 0)
        {
            var c = new PlanContext
            {
                Chapter = S.Chapter, ChapterShift = S.ChapterShift + ahead, LastStars = S.LastStars, HeatLeft = S.HeatLeft, HeatPct = S.HeatPct, TreeTrim = S.TreeTrim
            };
            foreach (var d in Gd.Districts) if (S.IsHooked(d.Id)) c.Hooked.Add(d.Id);
            foreach (var kv in S.Plants) c.Plants[kv.Key] = kv.Value;
            if (ahead == 0) c.Rush.AddRange(S.Rush);
            return c;
        }

        /// <summary>Forecast card: the next shift (forecast_plus / premium: 3). The weather chain is seed-only.</summary>
        public List<ShiftPlan> Forecast()
        {
            var o = new List<ShiftPlan>();
            int n = Perk("forecast_plus") || Perk("premium") ? 3 : 1;
            string wx = S.WxPrev;
            for (int k = 0; k < n; k++)
            {
                wx = Planner.WeatherKindFor(Gd, S.Seed, S.ShiftNo + k, wx);
                o.Add(Planner.Make(Gd, S.Seed, S.ShiftNo + k, wx, PlanCtx(k)));
            }
            return o;
        }

        /// <summary>weather / force: probes and tests only (a fixed weather kind, these event ids instead of the random roll).</summary>
        public bool StartShift(string weather = null, List<string> force = null)
        {
            if (S.Phase != "idle") return false;
            string wx = weather ?? Planner.WeatherKindFor(Gd, S.Seed, S.ShiftNo, S.WxPrev);
            var pc = PlanCtx(0);
            pc.Force = force;
            S.Plan = Planner.Make(Gd, S.Seed, S.ShiftNo, wx, pc);
            Grid = new Grid();
            Grid.Setup(Gd, S.Plan, Ctx());
            Grid.TutorialSafe = S.TutorialSafe;
            S.Phase = "live";
            S.Log.Clear();
            S.Pending.Clear();
            S.AccUs = 0;
            Emit(new SimEvent("shift_start").With("shift", S.ShiftNo).With("weather", wx));
            return true;
        }

        public void SetPaused(bool p) => S.Paused = p;

        /// <summary>Real seconds since the last frame. Runs whole 0.25 s steps; the remainder waits (integer microseconds).</summary>
        public void Advance(double dt)
        {
            if (double.IsNaN(dt) || dt < 0) dt = 0;
            S.Clock += dt;
            if (S.Phase == "live" && S.Paused && Grid != null && S.Pending.Count > 0) ApplyPendingNow();
            if (S.Phase != "live" || S.Paused || Grid == null) return;
            long us = (long)(dt * 1000000.0);
            S.Stats.PlayUs += us;
            S.AccUs += us * S.Speed;
            while (S.AccUs >= Gd.StepUs && S.Phase == "live")
            {
                S.AccUs -= Gd.StepUs;
                StepOnce();
            }
        }

        /// <summary>Runs n fixed steps of the live shift (tests / balance; identical to Advance).</summary>
        public void RunSteps(int n)
        {
            for (int i = 0; i < n && S.Phase == "live"; i++) StepOnce();
        }

        void StepOnce()
        {
            // the dispatcher assistant (owned + switched on) queues the same commands a player would
            if (Grid.Step % 4 == 0)
                foreach (var c in Dispatcher.AutoCommands(this)) S.Pending.Add(c);
            foreach (var c in S.Pending)
                if (Grid.Apply(c)) S.Log.Add(new KeyValuePair<int, Cmd>(Grid.Step, c));
            S.Pending.Clear();
            Grid.AdvanceStep();
            foreach (var e in Grid.Events)
            {
                events.Add(e);
                if ((e.Type == "repaired" || (e.Type == "restore" && !J.B(e.Data, "auto"))) && !S.Wow.ContainsKey("first_restore"))
                    WowMoment("first_restore", "first_district_restored");
            }
            Grid.Events.Clear();
            if (Grid.Finished) EndShift();
        }

        /// <summary>
        /// The review-gate hook (games/CLAUDE.md section 12, owner 10-27): a success moment of the FIRST session —
        /// "first_shift_completed" (shift 1 is ~4 min at 1x) and "first_district_restored". Emitted once each with the play
        /// time; the UI's ReviewGate decides (MIN_PLAY_S ~240, never in the guide / a modal / the shop).
        /// </summary>
        void WowMoment(string key, string reason)
        {
            long playS = S.Stats.PlayUs / 1000000;
            S.Wow[key] = (int)Math.Min(int.MaxValue, playS);
            Emit(new SimEvent("review_moment").With("reason", reason).With("play_s", playS).With("tutorial", S.TutorialSafe)
                .With("ready", playS >= Gd.ReviewMinPlayS && !S.TutorialSafe));
        }

        /// <summary>
        /// While the clock is paused (a guide tip waits, an ad shows) the player's orders still take effect at once: they
        /// are applied at the current step, as the next step would have applied them (same step index in the log, so a
        /// replay is unchanged). Device pass 10-28: a "+" tapped under a tip did nothing, the tip asked "Tap + again" and
        /// the Grid row kept the old setpoint.
        /// </summary>
        public void ApplyPendingNow()
        {
            if (S.Phase != "live" || Grid == null || S.Pending.Count == 0) return;
            foreach (var c in S.Pending)
                if (Grid.Apply(c)) S.Log.Add(new KeyValuePair<int, Cmd>(Grid.Step, c));
            S.Pending.Clear();
        }

        /// <summary>Player command: validated now, applied at the next fixed step (at once while paused).</summary>
        public QueueResult Queue(Cmd cmd)
        {
            if (S.Phase != "live" || Grid == null) return new QueueResult { Reason = "not_live" };
            if (cmd == null || Array.IndexOf(Cmd.Types, cmd.Type) < 0) return new QueueResult { Reason = "unknown" };
            S.Pending.Add(cmd.Copy());
            double eff = 0;
            if (cmd.Type == "ramp" && Gd.Plant.TryGetValue(cmd.Plant ?? "", out var p)) eff = p.DelayS;
            return new QueueResult { Ok = true, ApplyStep = Grid.Step, EffectInS = eff };
        }

        void EndShift()
        {
            var g = Grid;
            var acc = g.Acc;
            var rep = new ShiftReport { Shift = S.ShiftNo, Chapter = S.Chapter, Weather = S.Plan.Weather };
            Economy.ShiftMoney(this, g, rep);
            int uptime = (int)(acc.ServedWh * 1000 / Math.Max(acc.DemWh, 1));
            int stars = 1;
            if (uptime >= Gd.ThreeStarPm && rep.Profit > 0) stars = 3;
            else if (uptime >= Gd.TwoStarPm) stars = 2;
            double co2 = (g.PGenWh[Grid.PSteam] * Gd.Plant["steam"].Co2 + g.PGenWh[Grid.PGas] * Gd.Plant["gas"].Co2) / Math.Max((double)acc.ServedWh, 1.0);
            int co2Stars = co2 <= Gd.Co2Three ? 3 : (co2 <= Gd.Co2Two ? 2 : 1);
            int shiftRating = Math.Max(0, Math.Min(500, 500 * (uptime - Gd.RatingZeroPm) / (Gd.RatingFullPm - Gd.RatingZeroPm)));
            long dark = 0;
            foreach (var v in g.DDarkSteps) dark += v;
            rep.UptimePm = uptime;
            rep.ServedMwh = acc.ServedWh / 1000000;
            rep.DemandMwh = acc.DemWh / 1000000;
            rep.Stars = stars;
            rep.Co2Stars = co2Stars;
            rep.OutageMin = (int)(dark * 3 / 2);
            rep.Shed = (int)(acc.Shed + acc.AutoShed);
            rep.Faults = (int)acc.Faults;
            rep.Repairs = (int)acc.Repairs;
            rep.Trips = (int)(acc.Trips + acc.LineTrips);
            rep.BandPm = (int)(acc.BandSteps * 1000 / Math.Max(g.Steps, 1));
            foreach (var e in S.Plan.Events) rep.Events.Add(e.Id);
            rep.Negative = Planner.NegativeCount(Gd, S.Plan);
            rep.RatingX100 = shiftRating;
            rep.Hash = ShiftHash();
            S.Report = rep;
            S.Carry = g.MakeCarry();
            S.WxPrev = S.Plan.Weather;
            S.HeatLeft = S.Plan.HeatLeft;
            S.HeatPct = S.Plan.HeatPct;
            S.Rush.Clear();
            S.LastStars = stars;
            S.LastNet = rep.Profit - rep.Bonus;
            S.RatingHist.Add(shiftRating);
            while (S.RatingHist.Count > Gd.RatingWindow) S.RatingHist.RemoveAt(0);
            int sum = 0;
            foreach (var v in S.RatingHist) sum += v;
            S.RatingX100 = sum / S.RatingHist.Count;
            var keys = new List<string>(S.Hooked.Keys);
            foreach (var k in keys) S.Hooked[k] = S.Hooked[k] + 1;
            S.Stats.Shifts++;
            if (stars == 3) S.Stats.Stars3++;
            if (S.Chapter == 5 && S.Ch5Done < 0)
            {
                var goal = Gd.Chapters[Gd.Chapters.Count - 1];
                if (stars >= goal.GoalMinStars) S.Ch5Good++;
                if (S.Ch5Good >= goal.GoalGoodShifts && S.RatingX100 >= (int)(goal.GoalRating * 100.0))
                {
                    S.Ch5Done = S.ShiftNo;
                    Emit(new SimEvent("chapter_done").With("chapter", 5));
                }
            }
            S.Phase = "report";
            S.Shift = null;
            Emit(new SimEvent("shift_done").With("shift", rep.Shift).With("stars", stars).With("profit", rep.Profit).With("uptime_pm", uptime));
            if (!S.Wow.ContainsKey("first_shift")) WowMoment("first_shift", "first_shift_completed");
            Grid = null;
        }

        /// <summary>Report -> cash (the shift's profit, bonus included). Returns the amount.</summary>
        public long Collect()
        {
            if (S.Phase != "report" || S.Report == null) return 0;
            long amount = S.Report.Profit;
            S.Cash += amount;
            S.Stats.Earned += amount;
            S.Reports.Add(S.Report);
            while (S.Reports.Count > 10) S.Reports.RemoveAt(0);
            S.ShiftNo++;
            S.ChapterShift++;
            S.Phase = "idle";
            return amount;
        }

        /// <summary>Same seed + same commands = same hash (in process and across processes). FNV-1a 64 over the canonical JSON.</summary>
        public string ShiftHash()
        {
            if (Grid == null) return S.Report != null ? S.Report.Hash : "";
            var log = new List<object>();
            foreach (var kv in S.Log) log.Add(new JObj { ["step"] = (long)kv.Key, ["cmd"] = kv.Value.ToJson() });
            string text = Json.Write(new List<object> { Grid.ToJson(), S.Plan.ToJson(), log });
            return Fnv64(text);
        }

        public static string Fnv64(string text)
        {
            ulong h = 14695981039346656037UL;
            foreach (char c in text)
            {
                h ^= c;
                h *= 1099511628211UL;
            }
            return h.ToString("x16");
        }

        // ------------------------------------------------------------------------------------------- meta

        public bool Buy(string id) => Economy.Buy(this, id);
        public bool Hookup(string id) => Economy.Hookup(this, id);
        public OfflineSummary Offline(long nowUnix) => Economy.Offline(this, nowUnix);
        public void Touch(long nowUnix) => S.LastSeen = Math.Max(S.LastSeen, nowUnix);
        public RewardResult RewardOffer(string placement, long nowUnix) => Economy.RewardOffer(this, placement, nowUnix);
        public RewardResult GrantReward(string placement, long nowUnix, int fault = -1) => Economy.GrantReward(this, placement, nowUnix, fault);
        public FundsResult GrantPurchaseFunds(string txnId, string product) => Economy.GrantPurchaseFunds(this, txnId, product);
        public long DayIncome() => Economy.DayIncome(this);

        /// <summary>Speeds 1/2/3 free, 4 with the speed4 perk (integer compare; LL Driftwood P7).</summary>
        public bool SetSpeed(int x)
        {
            if (Gd.HasSpeed(x) || (x == Gd.Speed4 && Perk("speed4")))
            {
                S.Speed = x;
                return true;
            }
            return false;
        }

        public void SetAuto(string part, bool on) => S.AutoOn[part] = on;

        internal void Emit(SimEvent e) => events.Add(e);

        public List<SimEvent> DrainEvents()
        {
            var o = new List<SimEvent>(events);
            events.Clear();
            return o;
        }

        /// <summary>The save (SaveCodec.Encode wraps this).</summary>
        public JObj Snapshot()
        {
            if (Grid != null) S.Shift = Grid.ToJson();
            var j = S.ToJson(Gd);
            S.Shift = null;
            return j;
        }
    }
}
