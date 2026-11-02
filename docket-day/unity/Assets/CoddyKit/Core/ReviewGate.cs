// CoddyKit (Unity) — when the game may ask for a Play rating (games/CLAUDE.md §12, owner rule 2026-10-27; Corrupt
// src/game/ReviewGate.gd model). The ask itself is ONLY the OS sheet (Google Play In-App Review) — never a "do you like
// it / how many stars" card first (review gating is forbidden), never a reward for rating; the OS may still show nothing.
//   * FIRST SESSION is fine: >= MinPlayS (~240 s, game range 180-300) of real play (outside the guide and windows);
//   * only at a wow moment the game reports (Volt City: the first 3-star shift with a storm / peak event, or the first
//     newly hooked-up district lighting up — IAP.md §4; its older "2 sessions + 15 min" line is superseded by §12);
//   * never during the guide / onboarding, while a window / shop / purchase / ad is open, or in the background;
//   * at most 3 attempts, >= 30 days apart; every call counts as an attempt (the result is unknown by design).
using System.Collections.Generic;

namespace CoddyKit
{
    public sealed class ReviewGate
    {
        public const int MinSessions = 1;
        public double MinPlayS = 240.0;
        public const int MaxAsks = 3;
        public const double GapS = 30.0 * 86400.0;

        public int Sessions;
        public double PlayS;
        public int Asks;
        public double LastAsk;

        public struct Context
        {
            public bool Tutorial;
            public bool WindowOpen;   // modal / sheet / shop / purchase flow / interstitial / rewarded video
            public bool Background;
        }

        public void SessionStarted() { Sessions++; }

        public void Tick(double dt)
        {
            if (dt > 0 && dt < 5) PlayS += dt;
        }

        /// <summary>"" when the OS sheet may be requested now for this wow moment, else the reason.</summary>
        public string Blocker(Context c, double now)
        {
            if (c.Tutorial) return "tutorial";
            if (c.WindowOpen) return "window_open";
            if (c.Background) return "background";
            if (Sessions < MinSessions) return "sessions";
            if (PlayS < MinPlayS) return "play_time";
            if (Asks >= MaxAsks) return "max_asks";
            if (Asks > 0 && now - LastAsk < GapS) return "gap";
            return "";
        }

        public bool MayAsk(Context c, double now) => Blocker(c, now) == "";

        public void Asked(double now)
        {
            Asks++;
            LastAsk = now;
        }

        public Dictionary<string, object> ToJson() => new Dictionary<string, object>
        {
            { "v", 1L }, { "sessions", (long)Sessions }, { "play_s", PlayS }, { "asks", (long)Asks }, { "last_ask", LastAsk },
        };

        public static ReviewGate FromJson(Dictionary<string, object> j)
        {
            var g = new ReviewGate();
            if (j == null) return g;
            g.Sessions = MiniJson.Int(j, "sessions");
            g.PlayS = MiniJson.Num(j, "play_s");
            g.Asks = MiniJson.Int(j, "asks");
            g.LastAsk = MiniJson.Num(j, "last_ask");
            return g;
        }
    }
}
