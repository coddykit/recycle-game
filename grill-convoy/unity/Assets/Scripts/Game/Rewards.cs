// Grill Convoy — opt-in rewarded videos (design §10, fleet §4): AdMob rewarded ONLY, through the kit's RewardedController
// (UMP consent -> the one MobileAds.Initialize -> load; caps + cooldowns in coddyads/rewarded.json) AND the sim's own
// reward state (caps, cooldowns, context, cash caps — persisted in the career save). Both must allow it.
//   offline_x2      ×2 offline pay (bounded)                1/day            cash   (welcome-back card)
//   extra_runner    a helper on one truck for 60 game min    3/day, 15 min    —      (truck sheet, live)
//   bonus_patties   +20 patties on one truck                 3/day, 10 min    —      (truck sheet)
//   quick_prep      the commissary batch in progress done    2/day, 20 min    —      (commissary sheet)
//   crisis_rescue   flare-up out / propane back on a truck   2/day, 20 min    —      (truck sheet; crisis last resort)
//   daily_bonus     small cash                               1/day            cash   (morning plan)
// Units for the kit budget = milli-days of income (cash_s = 1 day = 1000): single ≤ 200 (1/5 of the cheapest pack),
// daily ≤ 500 (1/2). Cash rewards ask for their real size; non-cash ones for 1 unit. ad_free players get the same capped
// reward WITHOUT a video (rewarded_skipped_paid_user). Never offered in the guide (sim side). Nothing sold is given here.
using System;
using System.Collections.Generic;
using CoddyKit.Ads;
using GrillConvoy.Sim;

namespace GrillConvoy.Game
{
    public sealed class RewardOffer
    {
        public bool Show;
        public double Amount;
        public bool Video;
        public int Units;
    }

    public sealed class GrillRewards
    {
        public const int UnitsPerDay = 1000;
        public const int UnitsSingleMax = 200;
        public const int UnitsDailyMax = 500;
        public static readonly string[] Placements = Simulation.RewardPlacements;

        /// <summary>The kit's per-placement caps (= balance.json rewards; test-checked). Passed to the kit at boot.</summary>
        public static Dictionary<string, RewardedLimit> KitLimits() => new Dictionary<string, RewardedLimit>
        {
            { "offline_x2", new RewardedLimit(1, 0) },
            { "extra_runner", new RewardedLimit(3, 900) },
            { "bonus_patties", new RewardedLimit(3, 600) },
            { "quick_prep", new RewardedLimit(2, 1200) },
            { "crisis_rescue", new RewardedLimit(2, 1200) },
            { "daily_bonus", new RewardedLimit(1, 0) },
        };

        readonly Func<Simulation> sim;
        public readonly RewardedController Ctl;
        public Func<long> Now = GameSession.UnixNow;
        public Func<bool> AdFree = () => false;
        public event Action<string, RewardResult> Granted;

        public GrillRewards(Func<Simulation> sim, RewardedController ctl)
        {
            this.sim = sim;
            Ctl = ctl;
        }

        int UnitsFor(string placement, double amount)
        {
            if (!Simulation.RewardIsCash(placement)) return 1;
            double day = Math.Max(1, sim().DayIncome());
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
                case "extra_runner": return loc.T("reward.done_runner");
                case "bonus_patties": return loc.F("reward.done_patties", "n", (long)Math.Round(r.Amount));
                case "quick_prep": return loc.T("reward.done_prep");
                case "crisis_rescue": return loc.T("reward.done_rescue");
            }
            return loc.F("reward.done_cash", "cash", Fmt.Money((long)Math.Round(r.Amount), loc.Lang));
        }
    }
}
