// Volt City P4 — opt-in rewarded videos (docs/IAP.md section 3, games/CLAUDE.md §4): AdMob rewarded ONLY, through the kit's
// RewardedController (UMP consent -> the one MobileAds.Initialize -> load; caps + cooldowns persisted in coddyads/rewarded.json)
// AND the sim's own reward state (caps, cooldowns, context, cash caps — persisted in the career save). Both must allow it.
//   offline_x2        double the night-crew pay        1/day            cash  (Offline card)
//   shift_bonus       +50 % of the shift profit        3/day, 10 min    cash  (shift report)
//   emergency_import  60 s of imported power, no cash  2/day, 15 min    — "last chance" in a live deficit (Grid tab)
//   crew_overtime     instant repair of one fault      3/day, 5 min     — (fault card)
// Reward units for the kit budget = milli-days of income (cash_s = 1 day = 1000): single <= 200 (1/5 of the cheapest pack),
// daily <= 500 (1/2). Cash rewards ask for their real size in units; non-cash ones for 1 unit. ad_free players get the
// same capped reward WITHOUT a video (rewarded_skipped_paid_user). Never offered in the guide (sim: TutorialSafe), never
// while a sheet the reward does not belong to is open; the reward is granted only after the SDK's earned + closed.
// Nothing sold is ever given here (no cosmetics, speed, automation, slots, ad-free).
using System;
using System.Collections.Generic;
using CoddyKit;
using CoddyKit.Ads;
using VoltCity.Sim;

namespace VoltCity.Game
{
    public sealed class RewardOffer
    {
        public bool Show;
        public long Amount;
        public bool Video;
        public int Units;
    }

    public sealed class VoltRewards
    {
        public const int UnitsPerDay = 1000;
        public const int UnitsSingleMax = 200;
        public const int UnitsDailyMax = 500;
        public static readonly string[] Placements = { "offline_x2", "shift_bonus", "emergency_import", "crew_overtime" };

        /// <summary>The kit's per-placement caps (= balance.json rewards.placements; test-checked). VoltGameConfig passes
        /// these to the kit at boot, before the data files are read.</summary>
        public static Dictionary<string, RewardedLimit> KitLimits() => new Dictionary<string, RewardedLimit>
        {
            { "offline_x2", new RewardedLimit(1, 0) },
            { "shift_bonus", new RewardedLimit(3, 600) },
            { "emergency_import", new RewardedLimit(2, 900) },
            { "crew_overtime", new RewardedLimit(3, 300) },
        };

        public static bool IsCash(string placement) => placement == "offline_x2" || placement == "shift_bonus";

        readonly Func<Simulation> sim;
        public readonly RewardedController Ctl;
        public Func<long> Now = GameSession.UnixNow;
        public Func<bool> AdFree = () => false;
        /// <summary>(placement, result) after the sim granted it (toast, sound, save).</summary>
        public event Action<string, RewardResult> Granted;

        public VoltRewards(Func<Simulation> sim, RewardedController ctl)
        {
            this.sim = sim;
            Ctl = ctl;
        }

        int UnitsFor(string placement, long amount)
        {
            if (!IsCash(placement)) return 1;
            long day = Math.Max(1, sim().DayIncome());
            return (int)Math.Max(1, Math.Min(int.MaxValue, (amount * UnitsPerDay + day - 1) / day));
        }

        /// <summary>Whether the button may be shown now, what it gives and whether a video plays.</summary>
        public RewardOffer Get(string placement)
        {
            var o = new RewardOffer();
            var s = sim();
            if (s == null || Ctl == null) return o;
            var off = s.RewardOffer(placement, Now());
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

        /// <summary>Starts the flow (video or, for ad_free, at once). False = not available now.</summary>
        public bool Claim(string placement, int fault = -1)
        {
            var o = Get(placement);
            if (!o.Show) return false;
            return Ctl.Show(placement, o.Units, _ =>
            {
                var r = sim().GrantReward(placement, Now(), fault);
                Granted?.Invoke(placement, r);
            });
        }

        /// <summary>The opt-in row for a card / tab ("" placement text keys in strings.csv), or null when not offered.</summary>
        public Row Row(string placement, Loc loc, int fault = -1)
        {
            var o = Get(placement);
            if (!o.Show) return null;
            string key = placement == "offline_x2" ? "offline" : placement == "shift_bonus" ? "bonus" : placement == "emergency_import" ? "import" : "overtime";
            string label = IsCash(placement)
                ? loc.F(o.Video ? "reward.video_cash" : "reward.free_cash", "cash", Fmt.Money(o.Amount, loc.Lang))
                : loc.T(o.Video ? "reward.video" : "reward.claim");
            string id = "reward:" + placement + (fault >= 0 ? ":" + fault : "");
            var r = new Row
            {
                Id = "rw:" + placement, Icon = placement == "crew_overtime" ? "truck" : placement == "emergency_import" ? "bolt" : "star",
                Title = loc.T("reward." + key + ".t"), Sub = o.Video ? loc.T("reward." + key + ".b") : loc.T("reward.free.b"),
                Target = "reward." + placement,
            };
            r.Act(id, label, true, "tonal", "reward." + placement + ".claim");
            return r;
        }

        /// <summary>Toast text for a granted reward.</summary>
        public static string GrantedText(string placement, RewardResult r, Loc loc)
        {
            if (r == null || !r.Ok) return loc.T("reward.unavailable");
            if (placement == "emergency_import") return loc.T("reward.done_import");
            if (placement == "crew_overtime") return loc.T("reward.done_overtime");
            return loc.F("reward.done_cash", "cash", Fmt.Money(r.Amount, loc.Lang));
        }
    }

    /// <summary>The P4 services a view model may show (null = P3 behaviour: no shop rows, no reward rows).</summary>
    public sealed class Extras
    {
        public Commerce Shop;
        public VoltRewards Rewards;
    }
}
