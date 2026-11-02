// CoddyKit (Unity) — opt-in rewarded video caps (games/CLAUDE.md §4, CoddyAds.gd W45). Pure + persisted:
//   * per placement: daily cap (UTC day) + cooldown, persisted (a restart does not reset them; a clock set back never locks
//     an offer longer than one cooldown);
//   * reward units: single <= 1/5 and daily <= 1/2 of the cheapest consumable pack (the game sets Budget);
//   * Amount(wanted) = min(wanted, single cap, what is left of today's budget);
//   * Firebase params {placement, reward_amount, daily_index} for rewarded_offer_shown / _offer_accepted / _completed /
//     _skipped_paid_user.
// The video itself (AdMob, rewarded ONLY) is CoddyKit.Runtime CoddyRewarded + the AdMob bridge; ad_free players get the same
// capped reward without a video. Never grant through this what the game sells (§4.2).
using System;
using System.Collections.Generic;

namespace CoddyKit.Ads
{
    public struct RewardedLimit
    {
        public int Daily;
        public double CooldownS;
        public RewardedLimit(int daily, double cooldownS) { Daily = daily; CooldownS = cooldownS; }
    }

    public static class RewardedEvents
    {
        public const string OfferShown = "rewarded_offer_shown";
        public const string OfferAccepted = "rewarded_offer_accepted";
        public const string Completed = "rewarded_completed";
        public const string SkippedPaidUser = "rewarded_skipped_paid_user";
    }

    public sealed class RewardedRules
    {
        public static readonly RewardedLimit DefaultLimit = new RewardedLimit(2, 1800.0);
        public readonly Dictionary<string, RewardedLimit> Limits = new Dictionary<string, RewardedLimit>();
        /// <summary>Reward units; 0 = no unit cap (the placement caps still apply).</summary>
        public int BudgetSingle;
        public int BudgetDaily;
        const string UnitsKey = "_units";

        /// <summary>placement -> {day, count, last}; "_units" -> {day, count}. Persist with MiniJson (rewarded.json).</summary>
        public Dictionary<string, object> Usage;
        readonly IClock clock;

        public RewardedRules(IClock clock, Dictionary<string, object> usage = null)
        {
            this.clock = clock;
            Usage = usage ?? new Dictionary<string, object>();
        }

        RewardedLimit Limit(string placement) => Limits.TryGetValue(placement, out var l) ? l : DefaultLimit;
        string Today => UtcDay.Of(clock.UnixNow);

        int UsedToday(string key)
        {
            var e = MiniJson.Obj(Usage, key);
            return e != null && MiniJson.Str(e, "day") == Today ? MiniJson.Int(e, "count") : 0;
        }

        public int Remaining(string placement) => Math.Max(0, Limit(placement).Daily - UsedToday(placement));

        public double CooldownLeft(string placement)
        {
            double cd = Limit(placement).CooldownS;
            double last = MiniJson.Num(MiniJson.Obj(Usage, placement), "last");
            if (last <= 0) return 0;
            return Math.Min(Math.Max(last + cd - clock.UnixNow, 0.0), cd);
        }

        public int Amount(int wanted)
        {
            int units = Math.Max(0, wanted);
            if (BudgetSingle > 0) units = Math.Min(units, BudgetSingle);
            if (BudgetDaily > 0) units = Math.Min(units, Math.Max(0, BudgetDaily - UsedToday(UnitsKey)));
            return units;
        }

        /// <summary>Caps allow an offer now (the runtime adds "ad loaded or ad_free" and "not showing").</summary>
        public bool Allows(string placement, int wanted) => Remaining(placement) > 0 && CooldownLeft(placement) <= 0 && Amount(wanted) > 0;

        public Dictionary<string, object> Params(string placement, int units)
        {
            return new Dictionary<string, object> { { "placement", placement }, { "reward_amount", (long)units }, { "daily_index", (long)(UsedToday(placement) + 1) } };
        }

        /// <summary>Counts one earned reward (called once, when the SDK reported the reward or for an ad_free player).</summary>
        public void Count(string placement, int units)
        {
            string today = Today;
            Usage[placement] = new Dictionary<string, object> { { "day", today }, { "count", (long)(UsedToday(placement) + 1) }, { "last", clock.UnixNow } };
            Usage[UnitsKey] = new Dictionary<string, object> { { "day", today }, { "count", (long)(UsedToday(UnitsKey) + units) } };
        }

        /// <summary>The fleet cap rule for a game's numbers: single <= 1/5 and daily <= 1/2 of the cheapest consumable.
        /// Returns "" when ok.</summary>
        public static string CheckBudget(double singleValue, double dailyValue, double cheapestPackValue)
        {
            if (cheapestPackValue <= 0) return "cheapest pack value must be > 0";
            if (singleValue > cheapestPackValue / 5.0 + 1e-9) return $"single reward {singleValue} > 1/5 of {cheapestPackValue}";
            if (dailyValue > cheapestPackValue / 2.0 + 1e-9) return $"daily rewards {dailyValue} > 1/2 of {cheapestPackValue}";
            return "";
        }
    }

    /// <summary>One rewarded show: the reward is granted exactly once, only after BOTH the SDK's earned callback and the
    /// close (some SDKs report the reward after the close); a failed show / half-watched video grants nothing.</summary>
    public sealed class RewardedShowState
    {
        public bool Earned, Closed, Granted, Failed;

        /// <summary>Returns true when the grant must happen now.</summary>
        public bool OnEarned()
        {
            if (Earned || Failed) return false;
            Earned = true;
            return TryGrant();
        }

        public bool OnClosed()
        {
            if (Closed) return false;
            Closed = true;
            return TryGrant();
        }

        public void OnFailed() { Failed = true; }

        bool TryGrant()
        {
            if (Earned && Closed && !Granted && !Failed)
            {
                Granted = true;
                return true;
            }
            return false;
        }
    }
}
