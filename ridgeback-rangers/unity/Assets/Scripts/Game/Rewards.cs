// Ridgeback Rangers — opt-in rewarded videos (plan §10, fleet §4): AdMob rewarded ONLY, through the kit's RewardedController
// (UMP consent -> the one MobileAds.Initialize -> load; caps + cooldowns in coddyads/rewarded.json) AND the sim's own
// reward state (caps, cooldowns, context, cash caps — persisted in the career save). Both must allow it.
//   dawn_double     ×2 overnight donations (bounded, ≤ 0.08 day)    2/day, 10 min   cash   (welcome-back card)
//   borrow_ranger   a ranger from the next ridge takes one pin      3/day, 10 min   0.04   (pin card, no free ranger)
//   warm_incubator  an egg hatches 10 minutes sooner                2/day, 10 min   0.05   (hatchery)
//   storm_shelter   every fence tile repaired once ("last resort")  1/day, 10 min   0.10   (fence / escape pin card)
//   scout_tip       reveals a dig site's hidden species (info)      2/day, 10 min   —      (hatchery dig chart)
// Units for the kit budget = milli-days of income (cash_s = 1 day = 1000): single ≤ 200 (1/5 of the cheapest pack),
// daily ≤ 500 (1/2): 2×80 + 3×40 + 2×50 + 1×100 + 2×1 = 482. Cash rewards ask for their real size, the others for their
// balance.json value. ad_free players get the same capped reward WITHOUT a video (rewarded_skipped_paid_user). Never offered
// in the guide (sim side). Nothing sold is given here (no cosmetics, no permanent slots, no speed, no ad-free).
using System;
using System.Collections.Generic;
using CoddyKit.Ads;
using RidgebackRangers.Sim;

namespace RidgebackRangers.Game
{
    public sealed class RewardOffer
    {
        public bool Show;
        public double Amount;
        public bool Video;
        public int Units;
    }

    public sealed class RidgebackRewards
    {
        public const int UnitsPerDay = 1000;
        public const int UnitsSingleMax = 200;
        public const int UnitsDailyMax = 500;
        public static readonly string[] Placements = Simulation.RewardPlacements;

        /// <summary>The kit's per-placement caps (= balance.json rewards; test-checked). Passed to the kit at boot.</summary>
        public static Dictionary<string, RewardedLimit> KitLimits() => new Dictionary<string, RewardedLimit>
        {
            { "dawn_double", new RewardedLimit(2, 600) },
            { "borrow_ranger", new RewardedLimit(3, 600) },
            { "warm_incubator", new RewardedLimit(2, 600) },
            { "storm_shelter", new RewardedLimit(1, 600) },
            { "scout_tip", new RewardedLimit(2, 600) },
        };

        readonly Func<Simulation> sim;
        public readonly RewardedController Ctl;
        public Func<long> Now = GameSession.UnixNow;
        public Func<bool> AdFree = () => false;
        public event Action<string, RewardResult> Granted;

        public RidgebackRewards(Func<Simulation> sim, RewardedController ctl)
        {
            this.sim = sim;
            Ctl = ctl;
        }

        int UnitsFor(string placement, double amount)
        {
            var s = sim();
            if (!Simulation.RewardIsCash(placement))
                return s != null && s.Gd.Rewards.TryGetValue(placement, out var def) ? Math.Max(1, (int)Math.Round(def.Days * UnitsPerDay)) : 1;
            double day = Math.Max(1, s.DayIncome());
            return (int)Math.Max(1, Math.Min(int.MaxValue, Math.Ceiling(amount * UnitsPerDay / day)));
        }

        /// <summary>Whether the button may be shown now, what it gives and whether a video plays.</summary>
        public RewardOffer Get(string placement, int target = -1)
        {
            var o = new RewardOffer();
            var s = sim();
            if (s == null || Ctl == null) return o;
            var off = s.RewardOffer(placement, Now(), target);
            if (!off.Ok) return o;
            int units = UnitsFor(placement, off.Amount);
            if (Ctl.Rules.Amount(units) < units) return o;   // the kit's daily budget would cut it: do not offer a smaller one
            if (!Ctl.Ready(placement, units)) return o;
            o.Show = true;
            o.Amount = off.Amount;
            o.Units = units;
            o.Video = !AdFree();
            return o;
        }

        /// <summary>Starts the flow (video, or at once for ad_free). False = not available now.</summary>
        public bool Claim(string placement, int target = -1)
        {
            var o = Get(placement, target);
            if (!o.Show) return false;
            return Ctl.Show(placement, o.Units, _ =>
            {
                var r = sim().GrantReward(placement, Now(), target);
                Granted?.Invoke(placement, r);
            });
        }

        /// <summary>Toast text for a granted reward.</summary>
        public static string GrantedText(string placement, RewardResult r, Loc loc)
        {
            if (r == null || !r.Ok) return loc.T("reward.unavailable");
            switch (placement)
            {
                case "borrow_ranger": return loc.T("reward.done_borrow");
                case "warm_incubator": return loc.T("reward.done_warm");
                case "storm_shelter": return loc.T("reward.done_shelter");
                case "scout_tip": return loc.T("reward.done_scout");
            }
            return loc.F("reward.done_cash", "cash", Fmt.Money((long)Math.Round(r.Amount), loc.Lang));
        }
    }
}
