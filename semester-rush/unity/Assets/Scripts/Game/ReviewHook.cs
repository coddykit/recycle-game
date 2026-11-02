// Semester Rush — wow moments for the in-app review ask (fleet §12; the kit's CoddyReview / ReviewGate decide WHEN: first
// session ok, ≥ 240 s real play, never in the guide / a window / the background, ≤ 3 asks ≥ 30 days apart, OS sheet only,
// no "do you like it" card, nothing given for a rating). This class only says WHICH moments are wow moments:
//   * the first week with zero late students (plan section 10) — ~4-5 minutes into the first session;
//   * a strong week (grade ≥ 0.8);   * a new campus zone opened;   * a semester closed.
// A moment that happens inside the guide is deferred to the first moment after it (Deferred, kept in settings).
using SemesterRush.Sim;

namespace SemesterRush.Game
{
    public sealed class ReviewHook
    {
        public string Deferred = "";

        /// <summary>The wow moment this event is, or "" (not one).</summary>
        public static string Moment(SimEvent e, Simulation sim)
        {
            switch (e.Type)
            {
                case "week_done":
                    if (e.Text == "exam") return "semester";
                    if (e.B == 0 && sim.S.History.Count > 0 && sim.S.History[sim.S.History.Count - 1].Trips > 0) return "zero_late_week";
                    return e.V >= 0.8 ? "good_week" : "";
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
