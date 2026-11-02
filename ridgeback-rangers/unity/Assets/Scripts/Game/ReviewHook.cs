// Ridgeback Rangers — wow moments for the in-app review ask (fleet §12; the kit's CoddyReview / ReviewGate decide WHEN: first
// session ok, ≥ 240 s real play, never in the guide / a window / the background, ≤ 3 asks ≥ 30 days apart, OS sheet only,
// no "do you like it" card, nothing given for a rating). This class only says WHICH moments are wow moments:
//   * the first full Park Day with a good report (≥ 6 of 9 stars) — ~4 minutes into the first session, right after the guide;
//   * the first egg hatched;   * a new zone opened.
// A moment that happens inside the guide is deferred to the first moment after it (Deferred, kept in settings).
using RidgebackRangers.Sim;

namespace RidgebackRangers.Game
{
    public sealed class ReviewHook
    {
        public string Deferred = "";

        /// <summary>The wow moment this event is, or "" (not one).</summary>
        public static string Moment(SimEvent e, Simulation sim)
        {
            switch (e.Type)
            {
                case "day_done": return e.B >= 6 ? "good_day" : "";
                case "hatched": return sim.S.Hatched <= 1 ? "first_hatch" : "";
                case "zone": return "zone";
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
