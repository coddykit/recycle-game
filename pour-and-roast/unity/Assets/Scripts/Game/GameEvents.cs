// Pour & Roast — the analytics catalogue (Firebase project coddykit-game; fleet §4.9, §9, §14). Every game event goes
// through Validate: snake_case ≤ 40 chars, ≤ 25 params, no PII (no ids of people, no free text), numbers or short enums.
// Families / kids rules do not apply (13+ game). The kit's own events (rewarded_*, house ads, consent) use the same sink.
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace PourAndRoast.Game
{
    public static class GameEvents
    {
        public static readonly HashSet<string> Known = new HashSet<string>
        {
            // onboarding (fleet §9)
            "onboarding_begin", "onboarding_step", "onboarding_complete", "onboarding_skip",
            // rewarded (fleet §4.9; sent by the kit)
            "rewarded_offer_shown", "rewarded_offer_accepted", "rewarded_completed", "rewarded_skipped_paid_user",
            // saves (fleet §14)
            "save_unreadable", "save_failed",
            // shop
            "purchase_start", "purchase_success", "purchase_cancel", "purchase_fail", "restore_result", "offer_shown",
            // game
            "br_session", "br_day_done", "route_dispatch", "sim_purchase", "research_start", "district_unlock", "bale_sold",
            "crisis", "tip_shown", "settings_change", "review_moment", "gpu_shader_crash",
        };

        static readonly Regex Name = new Regex("^[a-z][a-z0-9_]{0,39}$");

        /// <summary>"" when the event may be sent, else why not.</summary>
        public static string Validate(string name, Dictionary<string, object> p)
        {
            if (string.IsNullOrEmpty(name) || !Name.IsMatch(name)) return "bad_name";
            if (!Known.Contains(name)) return "unknown_event";
            if (p == null) return "";
            if (p.Count > 25) return "too_many_params";
            foreach (var kv in p)
            {
                if (!Name.IsMatch(kv.Key)) return "bad_param " + kv.Key;
                if (kv.Value is string s && (s.Length > 40 || s.Contains("@"))) return "bad_value " + kv.Key;
                if (!(kv.Value is string || kv.Value is long || kv.Value is int || kv.Value is double || kv.Value is bool)) return "bad_type " + kv.Key;
            }
            return "";
        }
    }
}
