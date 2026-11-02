// Volt City P4 — the analytics event layer (Firebase later: project coddykit-game-2, own Android stream — Patron). Today the
// sink is the kit's NullAnalytics (no SDK, nothing leaves the phone); a Firebase bridge registering
// CoddyServices.Analytics at BeforeSceneLoad is wrapped by Install() automatically.
//   * ONE catalogue (Events): every event name + its allowed parameters (docs/ANALYTICS.md is the human copy). Kit events
//     (rewarded_*, review_prompt, gpu_shader_crash) and game events go through the same validator.
//   * Firebase limits enforced here, not discovered in the console: name <= 40 chars [A-Za-z][A-Za-z0-9_]*, not a reserved
//     name / prefix, <= 25 params, param names <= 40, string values <= 100 chars, values long / double / string only.
//   * no PII: ids of products / placements / districts and small numbers only (profit as a band, never raw cash).
// An unknown or invalid event is DROPPED and counted (Rejected) — a test reads it; nothing throws in a player's game.
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using CoddyKit;
using VoltCity.Sim;

namespace VoltCity.Game
{
    public sealed class VoltAnalytics : IAnalytics
    {
        public static readonly Dictionary<string, string[]> Events = new Dictionary<string, string[]>
        {
            // session / onboarding (P3 Coach)
            { "vc_session", new[] { "n", "tier" } },
            { "onboarding_step", new[] { "step" } },
            { "onboarding_assist", new[] { "target" } },
            { "onboarding_skip", new[] { "step" } },
            { "onboarding_complete", new[] { "wrong_taps" } },
            // career (sim events)
            { "shift_started", new[] { "shift", "speed" } },
            { "shift_completed", new[] { "shift", "stars", "uptime_pm", "profit_band" } },
            { "chapter_reached", new[] { "chapter" } },
            { "district_hooked", new[] { "district" } },
            { "investment_bought", new[] { "id" } },
            // saves (games/CLAUDE.md section 14) + device
            { "save_unreadable", new[] { "code", "recovered" } },
            { "save_failed", new[] { "code" } },
            { "gpu_shader_crash", new[] { "stage", "gpu" } },
            // rewarded (games/CLAUDE.md section 4.9; logged by the kit RewardedController)
            { RewardedEventsNames.OfferShown, new[] { "placement", "reward_amount", "daily_index" } },
            { RewardedEventsNames.OfferAccepted, new[] { "placement", "reward_amount", "daily_index" } },
            { RewardedEventsNames.Completed, new[] { "placement", "reward_amount", "daily_index" } },
            { RewardedEventsNames.SkippedPaidUser, new[] { "placement", "reward_amount", "daily_index" } },
            // shop
            { "shop_open", new[] { "source" } },
            { "purchase_start", new[] { "product" } },
            { "purchase_success", new[] { "product", "kind" } },
            { "purchase_cancel", new[] { "product" } },
            { "purchase_fail", new[] { "product", "reason" } },
            { "restore_result", new[] { "ok", "active" } },
            { "offer_shown", new[] { "offer" } },
            { "look_equip", new[] { "look", "on" } },
            // review (kit CoddyReview) + settings
            { "review_prompt", new[] { "moment", "attempt" } },
            { "settings_change", new[] { "setting", "value" } },
            { "city_slot", new[] { "slot", "fresh" } },
        };

        static readonly Regex NameRx = new Regex("^[A-Za-z][A-Za-z0-9_]{0,39}$");
        static readonly string[] Reserved =
        {
            "ad_activeview", "ad_click", "ad_exposure", "ad_impression", "ad_query", "adunit_exposure", "app_clear_data", "app_install",
            "app_remove", "app_update", "error", "first_open", "first_visit", "in_app_purchase", "notification_dismiss",
            "notification_foreground", "notification_open", "notification_receive", "os_update", "screen_view", "session_start",
            "user_engagement",
        };

        public readonly IAnalytics Sink;
        public readonly List<string> Rejected = new List<string>();
        public int Sent { get; private set; }

        public VoltAnalytics(IAnalytics sink) { Sink = sink ?? new NullAnalytics(); }

        /// <summary>Wraps whatever CoddyServices.Analytics is (NullAnalytics today, a Firebase bridge later). Call before the
        /// kit boots (the kit captures the sink when it builds the rewarded controller).</summary>
        public static VoltAnalytics Install()
        {
            if (CoddyServices.Analytics is VoltAnalytics v) return v;
            v = new VoltAnalytics(CoddyServices.Analytics);
            CoddyServices.Analytics = v;
            return v;
        }

        /// <summary>"" = valid, else why.</summary>
        public static string Validate(string name, Dictionary<string, object> p)
        {
            if (string.IsNullOrEmpty(name) || !NameRx.IsMatch(name)) return "bad_name";
            if (Array.IndexOf(Reserved, name) >= 0 || name.StartsWith("firebase_") || name.StartsWith("google_") || name.StartsWith("ga_")) return "reserved_name";
            if (!Events.TryGetValue(name, out var allowed)) return "unknown_event";
            if (p == null) return "";
            if (p.Count > 25) return "too_many_params";
            foreach (var kv in p)
            {
                if (!NameRx.IsMatch(kv.Key ?? "")) return "bad_param:" + kv.Key;
                if (Array.IndexOf(allowed, kv.Key) < 0) return "unknown_param:" + kv.Key;
                switch (kv.Value)
                {
                    case long _: case int _: case double _: case bool _: break;
                    case string s: if (s.Length > 100) return "long_value:" + kv.Key; break;
                    default: return "bad_value:" + kv.Key;
                }
            }
            return "";
        }

        public void Event(string name, Dictionary<string, object> parameters)
        {
            string why = Validate(name, parameters);
            if (why != "")
            {
                if (Rejected.Count < 100) Rejected.Add(name + ":" + why);
                return;
            }
            Sent++;
            Sink.Event(name, parameters);
        }

        /// <summary>Profit as a coarse band (no raw cash in analytics).</summary>
        public static string ProfitBand(long profit) =>
            profit < 0 ? "neg" : profit < 1000 ? "lt1k" : profit < 10000 ? "lt10k" : profit < 100000 ? "lt100k" : "ge100k";

        /// <summary>Career events worth a Firebase event (sim events -> name + params), or null.</summary>
        public static KeyValuePair<string, Dictionary<string, object>>? FromSim(SimEvent e, Simulation sim)
        {
            switch (e.Type)
            {
                case "shift_start":
                    return Pair("shift_started", new Dictionary<string, object> { { "shift", (long)(sim.S.ShiftNo + 1) }, { "speed", (long)sim.S.Speed } });
                case "shift_done":
                    return Pair("shift_completed", new Dictionary<string, object>
                    {
                        { "shift", e.Num("shift") + 1 }, { "stars", e.Num("stars") }, { "uptime_pm", e.Num("uptime_pm") }, { "profit_band", ProfitBand(e.Num("profit")) },
                    });
                case "chapter": return Pair("chapter_reached", new Dictionary<string, object> { { "chapter", e.Num("chapter") } });
                case "hooked_up": return Pair("district_hooked", new Dictionary<string, object> { { "district", e.Str("district") } });
                case "bought": return Pair("investment_bought", new Dictionary<string, object> { { "id", e.Str("id") } });
            }
            return null;
        }

        static KeyValuePair<string, Dictionary<string, object>>? Pair(string n, Dictionary<string, object> p) => new KeyValuePair<string, Dictionary<string, object>>(n, p);
    }

    /// <summary>The kit's rewarded event names (CoddyKit.Ads.RewardedEvents) under a game-side alias (no CoddyKit.Ads import here).</summary>
    static class RewardedEventsNames
    {
        public const string OfferShown = CoddyKit.Ads.RewardedEvents.OfferShown;
        public const string OfferAccepted = CoddyKit.Ads.RewardedEvents.OfferAccepted;
        public const string Completed = CoddyKit.Ads.RewardedEvents.Completed;
        public const string SkippedPaidUser = CoddyKit.Ads.RewardedEvents.SkippedPaidUser;
    }
}
