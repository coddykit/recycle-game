// Charge Nurse sim core — engine independent (no UnityEngine). C# 9 / .NET Standard 2.1.
// One career: a small-town clinic that grows into a city hospital (plan §3-§6). A shift runs 07:00-19:00 game time
// (~4 real minutes at 1x):
//   plan   (07:00, paused) — the morning board: forecast, staffing, rules, purchases;
//   live                   — patients arrive, the teams triage / scan / treat / admit / discharge / clean by the rules,
//                            the player overrides (tags, bays, discharge, clean, ambulances) and finds the bottleneck;
//   handover (18:30-19:00) — a calm handover resets the teams' fatigue; none = rushed (lost notes, tired teams);
//   night  (19:00-07:00)   — the night team runs the hospital by itself at reduced capacity (no player, no crises);
//   report                 — stars, reputation, money; NextDay().
// Partial class: this file = construction, queries, time, arrivals; Flow.cs = the patient pipeline + staff + bottleneck;
// Economy.cs = day end, purchases, research, offline, rewards; Crises.cs = events; Guide.cs = the guide's scripted moments.
using System;
using System.Collections.Generic;

namespace ChargeNurse.Sim
{
    public sealed class SimEvent
    {
        public string Type;     // arrive, triaged, treated, admitted, discharged, lwbs, worsened, transfer, ambulance, crisis, day_done, ...
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
        bool night;   // inside EndDay's night shift (atomic: never saved mid-night)
        /// <summary>Emit per-patient movement events for the diorama (off in balance bots).</summary>
        public bool EmitCrowd = true;

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
            return S.ToJson();
        }

        void NewCareer()
        {
            S.Day = 1;
            S.Minute = Gd.DayStart;
            S.Phase = Phases.Plan;
            S.Cash = Gd.StartCash;
            S.Reputation = Gd.StartReputation;
            foreach (var t in Teams.Pool) S.Teams[t] = Gd.StartTeams.TryGetValue(t, out int n) ? n : 0;
            foreach (var t in Teams.All) S.Fatigue[t] = 0;
            S.Wings.Add(Gd.Wings.Count > 0 ? Gd.Wings[0].Id : "clinic");
            foreach (var kv in Gd.StartBeds) for (int i = 0; i < kv.Value; i++) AddBed(Gd.Zone(kv.Key));
            S.Today = new DayStats { Day = 1 };
            // the guide's first patient: registered, waiting in the hall for a colour (plan §9 step 1)
            S.ArrivalsOn = false;
            var p = NewPatient(Sev.Green, false);
            p.Stage = Stage.Triage;
            p.Test = "";
            p.Admit = false;
            p.Guided = true;
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
        public bool Night => night;
        public bool Has(string research) => S.Research.Contains(research);
        public bool Unlocked(string wing) => S.Wings.Contains(wing);
        public bool ZoneOpen(int zone) => zone >= 0 && zone < Gd.Zones.Count && Unlocked(Gd.Zones[zone].Wing);
        public bool Perk(string id) => perks.TryGetValue(id, out bool b) && b;
        public int Chapter
        {
            get
            {
                int c = 1;
                foreach (var w in Gd.Wings) if (Unlocked(w.Id)) c = Math.Max(c, w.Chapter);
                return c;
            }
        }
        public int MaxSpeed => Perk("speed") ? Gd.PaidMaxSpeed : Gd.FreeMaxSpeed;
        /// <summary>Case payment multiplier from reputation (referrals and insurer contracts follow outcomes).</summary>
        public double PayMult => Gd.PayRepMin + Gd.PayRepSpan * Math.Max(0, Math.Min(100, S.Reputation)) / 100.0;
        public int HourIndex => Math.Max(0, Math.Min(Gd.HourCurve.Length - 1, (int)((S.Minute - Gd.DayStart) / 60.0)));
        public bool HandoverWindow => S.Phase == Phases.Live && S.Minute >= Gd.HandoverFrom && S.Minute < Gd.DayEnd;
        public int Team(string id) => S.Teams.TryGetValue(id ?? "", out int n) ? n : 0;
        public Patient P(int id) => S.Patients.Find(p => p.Id == id);
        public int PatientIndex(int id) => S.Patients.FindIndex(p => p.Id == id);
        public ZoneDef ZoneOf(Bed b) => Gd.Zones[b.Zone];
        public string WardOf(Bed b) => b.Bay ? "" : Gd.Zones[b.Zone].Ward;
        public Room RoomOf(string kind) => S.Rooms.Find(r => r.Kind == kind);
        public bool Built(string room) => RoomOf(room) != null;
        public int Bays => S.Beds.FindAll(b => b.Bay).Count;
        public int BedsIn(string ward) => S.Beds.FindAll(b => !b.Bay && WardOf(b) == ward).Count;
        public int WardBeds => S.Beds.FindAll(b => !b.Bay).Count;

        /// <summary>Where a patient is in the world: bed / room zone, else the zone of the queue they wait in.</summary>
        public int ZoneOfPatient(Patient p)
        {
            if (p.Bed >= 0 && p.Bed < S.Beds.Count) return S.Beds[p.Bed].Zone;
            if (p.Room >= 0 && p.Room < S.Rooms.Count) return S.Rooms[p.Room].Zone;
            switch (p.Stage)
            {
                case Stage.Reg: case Stage.Registering: return Gd.Zone("reception");
                case Stage.Triaging: return Gd.Zone("triage");
                case Stage.Test: return p.Test != "" ? Gd.TestZone(p.Test) : Gd.Zone("waiting");
                case Stage.Lounge: return Gd.Zone("lounge");
                case Stage.Fast: return Gd.Zone("triage");
                case Stage.Move: return p.Dest >= 0 && p.Dest < S.Beds.Count ? S.Beds[p.Dest].Zone : Gd.Zone("hall_c2");
            }
            return Gd.Zone("waiting");
        }

        /// <summary>Patients the waiting hall shows (registered, not with a doctor yet, not in a room), oldest first.</summary>
        public List<Patient> Waiting()
        {
            var l = new List<Patient>();
            foreach (var p in S.Patients) if ((p.Stage == Stage.Triage || p.Stage == Stage.Bay || p.Stage == Stage.Reg) && p.Bed < 0) l.Add(p);
            return l;
        }

        /// <summary>ED load for the diversion rule and the HUD: occupied bays + patients waiting for one, over the bays.</summary>
        public double EdLoad()
        {
            int bays = 0, used = 0;
            foreach (var b in S.Beds) if (b.Bay && b.State != BedState.Closed) { bays++; if (b.State != BedState.Clean) used++; }
            foreach (var p in S.Patients) if (p.Stage == Stage.Bay) used++;
            return bays == 0 ? 1 : (double)used / bays;
        }

        // ------------------------------------------------------------------------------------------------ time

        public void SetSpeed(int speed) => S.Speed = Math.Max(1, Math.Min(MaxSpeed, speed));
        public void SetPaused(bool paused) => S.Paused = paused;

        /// <summary>Store perks (never saved): premium, speed, gold_theme …</summary>
        public void SetPerks(Dictionary<string, bool> p)
        {
            perks.Clear();
            if (p != null) foreach (var kv in p) perks[kv.Key] = kv.Value;
            if (S.Speed > MaxSpeed) S.Speed = MaxSpeed;
        }

        /// <summary>Every frame with real seconds. The shift runs only while live and not paused.</summary>
        public void Advance(double realDt)
        {
            if (realDt <= 0 || double.IsNaN(realDt)) return;
            realDt = Math.Min(realDt, 1.0);   // a hitch never jumps the sim
            if (S.Phase != Phases.Live || S.Paused) return;
            AdvanceMinutes(realDt * Gd.GameMinPerRealS * Math.Max(1, S.Speed));
        }

        /// <summary>Advances game minutes directly (tests, guide fast-forward). Stops at the day end.</summary>
        public void AdvanceMinutes(double minutes)
        {
            while (minutes > 1e-9 && S.Phase == Phases.Live)
            {
                double step = Math.Min(1.0, minutes);
                Step(step);
                minutes -= step;
            }
        }

        void Step(double dt)
        {
            if (!night) RunPlanned();
            TickEvents(dt);
            Arrivals(dt);
            Flow(dt);
            Account(dt);
            if (!night)
            {
                TickResearch(dt);
                if (S.FloatLeft > 0) { S.FloatLeft -= dt; if (S.FloatLeft <= 0) { S.FloatLeft = 0; S.FloatTeam = ""; } }
                S.Minute += dt;
                if (S.Minute >= Gd.DayEnd - 1e-9) EndDay();
            }
        }

        // ------------------------------------------------------------------------------------------------ arrivals

        int Sample(double expected)
        {
            if (expected <= 0) return 0;
            int k = (int)Math.Floor(expected);
            if (Rng.Frand() < expected - k) k++;
            return k;
        }

        double WingArrivals()
        {
            double m = 0;
            foreach (var w in Gd.Wings) if (Unlocked(w.Id)) m = Math.Max(m, w.Arrivals);
            return Math.Max(0.5, m);
        }

        /// <summary>Walk-ins per game minute now (hour curve × reputation × wings × flu week; the night is quieter).</summary>
        public double ArrivalRate()
        {
            double rep = Gd.RepMin + Gd.RepSpan * Math.Max(0, Math.Min(100, S.Reputation)) / 100.0;
            double r = Gd.ArrivalsPerHour / 60.0 * rep * WingArrivals() * (night ? Gd.NightMult : Gd.HourCurve[HourIndex]);
            foreach (var e in S.Events) if (e.Id == "flu_surge") r *= e.Mult;
            return r;
        }

        void Arrivals(double dt)
        {
            // an unanswered call runs out: the default answer (accept, or the diversion rule)
            if (S.Ambulance != null && !S.Ambulance.Guided)
            {
                S.Ambulance.Left -= dt;
                if (S.Ambulance.Left <= 0) DecideAmbulance(DefaultAccept());
            }
            if (!S.ArrivalsOn) return;
            int n = Sample(ArrivalRate() * dt);
            for (int k = 0; k < n; k++) NewPatient(PickSev(Gd.Mix), false);
            // ambulance calls (the guide makes its own)
            if (!S.Tutorial && S.Ambulance == null)
            {
                double perDay = Gd.AmbulancePerDay[Math.Min(Chapter, Gd.AmbulancePerDay.Length - 1)];
                if (Rng.Frand() < perDay / Gd.DayMinutes * dt * (night ? Gd.NightMult : 1))
                {
                    S.Ambulance = new AmbulanceCall { Sev = PickSev(Gd.AmbulanceSev), Left = Gd.AmbulanceAnswerMin };
                    if (night) DecideAmbulance(DefaultAccept());
                    else Emit("ambulance_call", -1, S.Ambulance.Sev);
                }
            }
        }

        int PickSev(double[] w)
        {
            int s = Rng.PickWeighted(w);
            return s < 0 ? Sev.Green : s;
        }

        /// <summary>Unanswered call: accepted, unless the diversion rule (research) says the ED is too full.</summary>
        bool DefaultAccept() => !(Has("diversion_rules") && S.DivertAt > 0 && EdLoad() * 100 >= S.DivertAt);

        /// <summary>A new patient at the door (walk-in: registration first; ambulance: tagged by the crew, straight in).</summary>
        Patient NewPatient(int sev, bool ambulance)
        {
            var p = new Patient { Id = S.NextPatient++, Sev = sev, Amb = ambulance, Stage = Stage.Reg };
            if (Rng.Frand() < Gd.TestP[sev]) p.Test = PickTest(sev);
            p.Admit = Rng.Frand() < Gd.AdmitP[sev];
            if (p.Admit)
            {
                p.Ward = PickWard(sev);
                p.NeedsOr = Built("or") && Rng.Frand() < Gd.OrP[sev];
                if (p.NeedsOr) p.Ward = sev >= Sev.Red && Unlocked("city") ? "icu" : "surgical";
            }
            if (ambulance) { p.Tag = sev; p.Stage = p.Test != "" ? Stage.Test : Stage.Bay; S.Today.Ambulances++; }
            S.Patients.Add(p);
            S.Today.Arrivals++;
            if (EmitCrowd) Emit("arrive", p.Id, sev, 0, ambulance ? "amb" : "walk");
            return p;
        }

        string PickTest(int sev)
        {
            var opts = new List<string>();
            foreach (var r in S.Rooms) if (r.Kind != "or" && (r.Kind != "ct" || sev >= Sev.Orange)) opts.Add(r.Kind);
            return opts.Count == 0 ? "" : opts[Rng.RangeI(0, opts.Count - 1)];
        }

        string PickWard(int sev)
        {
            if (Unlocked("city") && sev >= Sev.Red && BedsIn("icu") > 0) return "icu";
            if (Unlocked("city") && sev >= Sev.Orange && BedsIn("surgical") > 0 && Rng.Frand() < 0.35) return "surgical";
            if (Unlocked("town") && sev >= Sev.Yellow && BedsIn("general") > 0) return "general";
            return BedsIn("obs") > 0 || !Unlocked("town") ? "obs" : "general";
        }

        /// <summary>The player's (or the default's) answer to the ambulance call.</summary>
        public string DecideAmbulance(bool accept)
        {
            var c = S.Ambulance;
            if (c == null) return "none";
            S.Ambulance = null;
            if (!accept)
            {
                S.Today.Diverted++;
                Emit("diverted", -1, c.Sev);
                return "";
            }
            var p = NewPatient(c.Sev, true);
            if (c.Guided)
            {
                // the guide's red patient needs the X-ray the player opens next (plan §9 steps 6-7)
                p.Test = "xray";
                p.Admit = false;
                p.Stage = Stage.Test;
            }
            Emit("ambulance", p.Id, c.Sev);
            return "";
        }
    }
}
