// Docket Day sim core — the day plan: walk-in minutes and crises (design §7, MVP four: surprise motion, power cut, sick
// day, substitute judge). Crises are short (today only), never game over, never during the guide or the first forgiving
// days; each one has a lever (reassign, visiting counsel, continuance, settle).
using System;
using System.Collections.Generic;

namespace DocketDay.Sim
{
    public sealed partial class Simulation
    {
        int Sample(double expected)
        {
            if (expected <= 0) return 0;
            int k = (int)Math.Floor(expected);
            if (Rng.Frand() < expected - k) k++;
            return k;
        }

        public double WalkinRate
        {
            get
            {
                double b = Gd.Walkins[Math.Max(0, Math.Min(Gd.Walkins.Length - 1, S.Chapter - 1))];
                return b + Gd.WalkinPerStanding * AvgStanding + Gd.WalkinPerReceptionist * Math.Min(Gd.MaxReceptionists, CountRole("receptionist"));
            }
        }

        void PlanDay()
        {
            S.Planned.Clear();
            if (S.Tutorial) return;
            int walkins = Sample(WalkinRate);
            for (int i = 0; i < walkins; i++) S.Planned.Add(new PlannedEvent { Id = "walkin", At = Gd.DayStart + 20 + Rng.Frand() * (Gd.DayMinutes - 110) });
            if (S.Day <= Gd.EventFreeDays) return;
            double expected = Gd.EventsPerDay[Math.Min(S.Chapter, Gd.EventsPerDay.Length - 1)];
            int n = Sample(expected);
            for (int k = 0; k < n; k++)
            {
                var pool = Gd.Events.FindAll(e => S.Day >= e.MinDay && !S.Planned.Exists(p => p.Id == e.Id));
                if (pool.Count == 0) break;
                var w = new double[pool.Count];
                for (int i = 0; i < w.Length; i++) w[i] = pool[i].Weight;
                var ev = pool[Math.Max(0, Rng.PickWeighted(w))];
                S.Planned.Add(new PlannedEvent { Id = ev.Id, At = Gd.DayStart + 30 + Rng.Frand() * (Gd.DayMinutes - 150) });
            }
        }

        void RunPlanned()
        {
            for (int i = S.Planned.Count - 1; i >= 0; i--)
            {
                if (i >= S.Planned.Count || S.Planned[i].At > S.Minute) continue;
                string id = S.Planned[i].Id;
                S.Planned.RemoveAt(i);
                if (id == "walkin") Walkin();
                else Fire(id);
            }
        }

        EventDef Def(string id) => Gd.Events.Find(e => e.Id == id) ?? new EventDef { Id = id, Hours = 1.5, Minutes = 60 };

        T Pick<T>(List<T> l) => l.Count == 0 ? default : l[Rng.RangeI(0, l.Count - 1)];

        /// <summary>Applies a crisis now (also used by tests and the balance bot). False when it found no target.</summary>
        public bool Fire(string id)
        {
            var e = Def(id);
            switch (id)
            {
                case "surprise_motion":
                {
                    var l = S.Matters.FindAll(m => m.Status == MatterStatus.Open && !m.Tutorial && m.HearingTask != null && m.HearingTask.Due > S.Day);
                    var m = Pick(l);
                    if (m == null) return false;
                    m.Tasks.Insert(m.Tasks.Count - 1, new TaskState { Kind = Kinds.Motion, Need = Math.Round(e.Hours * TierOf(m).Hours, 2), Due = Math.Min(S.Day + 1, m.HearingTask.Due) });
                    Emit("crisis", m.Id, -1, 0, id);
                    return true;
                }
                case "power_cut":
                    S.Events.Add(new ActiveEvent { Id = id, UntilDay = S.Day, From = S.Minute, To = S.Minute + e.Minutes });
                    Emit("crisis", -1, -1, e.Minutes, id);
                    return true;
                case "sick_day":
                {
                    var s = Pick(S.Staff.FindAll(x => !IsFounder(x) && !x.Sick && x.TempUntil <= 0 && !InSession(x.Id)));
                    if (s == null) return false;
                    s.Sick = true;
                    s.HoursLeft = 0;
                    S.Events.Add(new ActiveEvent { Id = id, UntilDay = S.Day, Target = s.Id });
                    Emit("crisis", s.Id, -1, 0, id);
                    return true;
                }
                case "substitute_judge":
                {
                    if (Gd.Judges.Count < 2) return false;
                    var m = Pick(S.Matters.FindAll(x => x.Status == MatterStatus.Open && !x.Tutorial && x.HearingTask != null && x.HearingTask.Due <= S.Day + 3));
                    if (m == null) return false;
                    int j = m.Judge;
                    for (int k = 0; k < 10 && j == m.Judge; k++) j = Rng.RangeI(0, Gd.Judges.Count - 1);
                    if (j == m.Judge) j = (m.Judge + 1) % Gd.Judges.Count;
                    m.Judge = j;
                    Emit("crisis", m.Id, j, 0, id);
                    return true;
                }
            }
            return false;
        }

        /// <summary>Crisis chips for the HUD (today).</summary>
        public List<string> ActiveCrises()
        {
            var l = new List<string>();
            foreach (var e in S.Events)
                if (e.UntilDay >= S.Day && !l.Contains(e.Id) && (e.Id != "power_cut" || (S.Minute >= e.From && S.Minute < e.To))) l.Add(e.Id);
            return l;
        }
    }
}
