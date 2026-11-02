// Grill Convoy — wow moments for the in-app review ask (fleet §12; the kit's CoddyReview / ReviewGate decide WHEN: first
// session ok, ≥ 240 s real play, never in the guide / a window / the background, ≤ 3 asks ≥ 30 days apart, OS sheet only,
// no "do you like it" card, nothing given for a rating). This class only says WHICH moments are wow moments:
//   * the first good day report (≥ 6 of 9 stars) — ~4 minutes into the first session, right after the guide;
//   * a new pitch unlocked (Boardwalk, Stadium);   * a halftime stampede beaten (≥ 85 % of the day's customers served).
// A moment that happens inside the guide is deferred to the first moment after it (Deferred, kept in settings).
using GrillConvoy.Sim;

namespace GrillConvoy.Game
{
    public sealed class ReviewHook
    {
        public string Deferred = "";

        /// <summary>The wow moment this event is, or "" (not one).</summary>
        public static string Moment(SimEvent e, Simulation sim)
        {
            switch (e.Type)
            {
                case "day_done":
                {
                    if (e.B >= 6) return "good_day";
                    var d = sim.S.History.Count > 0 ? sim.S.History[sim.S.History.Count - 1] : null;
                    return d != null && sim.MatchDay(d.Day) && d.Arrivals > 0 && d.LostRate <= 0.15 && sim.PitchOpen(sim.Gd.Pitches.Find(n => sim.Gd.Nodes[n].Match)) ? "rush_beaten" : "";
                }
                case "district": return "district";
            }
            return "";
        }

        /// <summary>Returns the moment to report to the kit now ("" = none); in the guide it is remembered instead.</summary>
        public string Check(SimEvent e, Simulation sim, bool inGuide)
        {
            string m = Moment(e, sim);
            if (m == "") return "";
            if (inGuide) { Deferred = m; return ""; }
            return m;
        }

        /// <summary>After the guide: the deferred moment (once).</summary>
        public string TakeDeferred(bool inGuide)
        {
            if (inGuide || Deferred == "") return "";
            string m = Deferred;
            Deferred = "";
            return m;
        }
    }
}
